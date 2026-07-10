using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using LexFlow.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexFlow.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Single choke point for audit capture (PRD §30: "EF Core SaveChanges interceptor
/// (single choke point)"). Writes one audit.audit_events row per tracked entity
/// mutation by adding new <see cref="AuditEvent"/> entities to the same DbContext's
/// change tracker inside SavingChanges — so they ride the same SaveChanges call, and
/// therefore the same database transaction, as the business change (§30 "Capture":
/// "same-transaction write — no async loss"). FR-004: create/update/delete only;
/// read-of-sensitive-record, login, export, approve and override events are written
/// explicitly by the code paths that perform them, not by this interceptor.
/// </summary>
public sealed class AuditSaveChangesInterceptor(IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Exact property names known to carry secrets on the current entity set (PRD §30:
    /// "redacted fields masked"; §29: "passwords, tokens ... never logged" — the same
    /// policy applies to the audit trail itself, not just application logs).
    /// </summary>
    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash",
        "TwoFaSecret",
        "RefreshHash",
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        WriteAuditEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        WriteAuditEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditEvents(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        // Snapshot first: we're about to Add() new AuditEvent entities to this same
        // change tracker, which would otherwise invalidate an in-flight enumeration.
        var mutatedEntries = context.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditEvent && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (mutatedEntries.Count == 0)
        {
            return;
        }

        var httpContext = httpContextAccessor.HttpContext;
        var (actorUserId, actorType) = ResolveActor(httpContext);
        var ip = httpContext?.Connection.RemoteIpAddress?.ToString();
        var ua = httpContext?.Request.Headers.UserAgent.ToString();
        var traceId = Activity.Current?.Id ?? httpContext?.TraceIdentifier;

        foreach (var entry in mutatedEntries)
        {
            var tenantId = ResolveTenantId(entry);
            if (tenantId is null)
            {
                // Every current entity is tenant-scoped (or, for core.tenants, IS the
                // tenant) — this branch exists only as a safety net for a future
                // entity type that forgets tenant_id; skip it rather than throw, since
                // a missing audit row is preferable to failing the business transaction.
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "create",
                EntityState.Modified => "update",
                EntityState.Deleted => "delete",
                _ => throw new InvalidOperationException($"Unexpected entity state '{entry.State}' reached audit capture."),
            };

            var (before, after) = BuildDiff(entry);
            var entityId = ResolveEntityId(entry);
            var entityType = $"{entry.Metadata.GetSchema()}.{entry.Metadata.GetTableName()}";

            var auditEvent = new AuditEvent(
                tenantId.Value,
                actorUserId,
                actorType,
                action,
                entityType,
                entityId,
                before,
                after,
                ip,
                ua,
                traceId);

            context.Set<AuditEvent>().Add(auditEvent);
        }
    }

    private static (Guid? ActorUserId, string ActorType) ResolveActor(HttpContext? httpContext)
    {
        if (httpContext?.User.Identity?.IsAuthenticated != true)
        {
            return (null, "system");
        }

        var sub = httpContext.User.FindFirst("sub")?.Value;
        var actorType = httpContext.User.FindFirst("actor_type")?.Value ?? "staff";
        return (Guid.TryParse(sub, out var userId) ? userId : null, actorType);
    }

    private static Guid? ResolveTenantId(EntityEntry entry)
    {
        if (entry.Metadata.FindProperty("TenantId") is not null
            && entry.Property("TenantId").CurrentValue is Guid tenantId)
        {
            return tenantId;
        }

        // core.tenants has no tenant_id column — the row's own id IS the tenant
        // (lexflow-database Scripts/02_Core/Tenants/001_Table.sql).
        if (entry.Entity is Tenant tenant)
        {
            return tenant.Id;
        }

        return null;
    }

    private static Guid? ResolveEntityId(EntityEntry entry)
    {
        var keyProperties = entry.Metadata.FindPrimaryKey()?.Properties ?? [];

        // Composite-PK join tables (user_roles, role_permissions, ...) have no single
        // identifying uuid — entity_id stays null and the key values still appear in
        // the before/after payload, satisfying §19.15's "entity type+id" as best it can
        // for a table that was never given a surrogate id.
        if (keyProperties.Count != 1 || keyProperties[0].ClrType != typeof(Guid))
        {
            return null;
        }

        return entry.Property(keyProperties[0].Name).CurrentValue as Guid?;
    }

    private static (string? Before, string? After) BuildDiff(EntityEntry entry)
    {
        var captureBefore = entry.State is EntityState.Modified or EntityState.Deleted;
        var captureAfter = entry.State is EntityState.Modified or EntityState.Added;

        Dictionary<string, object?>? before = captureBefore ? new Dictionary<string, object?>() : null;
        Dictionary<string, object?>? after = captureAfter ? new Dictionary<string, object?>() : null;

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;
            var redacted = IsSensitive(name);

            before?.Add(name, redacted ? "***REDACTED***" : property.OriginalValue);
            after?.Add(name, redacted ? "***REDACTED***" : property.CurrentValue);
        }

        return (
            before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            after is null ? null : JsonSerializer.Serialize(after, JsonOptions));
    }

    private static bool IsSensitive(string propertyName)
    {
        if (SensitivePropertyNames.Contains(propertyName))
        {
            return true;
        }

        var lowered = propertyName.ToLowerInvariant();
        return lowered.Contains("password") || lowered.Contains("secret") || lowered.Contains("token");
    }
}
