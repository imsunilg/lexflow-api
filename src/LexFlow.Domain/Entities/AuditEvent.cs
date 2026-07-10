namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to audit.audit_events (lexflow-database Scripts/10_Audit/AuditEvents).
/// Composite-PK (id, at) monthly-partitioned, insert-only table — no audit trio of
/// its own (this IS the audit trail; §30) and no FKs (§19.15: logical reference by
/// entity_type/entity_id only, since audit outlives the entities it describes).
/// Rows are written exclusively by <see cref="LexFlow.Infrastructure.Persistence.Interceptors.AuditSaveChangesInterceptor"/>
/// (or explicit calls for non-EF paths) — never mutated afterwards.
/// </summary>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid tenantId,
        Guid? actorUserId,
        string actorType,
        string action,
        string entityType,
        Guid? entityId,
        string? before,
        string? after,
        string? ip,
        string? ua,
        string? traceId)
    {
        Id = Guid.NewGuid();
        At = DateTimeOffset.UtcNow;
        TenantId = tenantId;
        ActorUserId = actorUserId;
        ActorType = actorType;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Before = before;
        After = after;
        Ip = ip;
        Ua = ua;
        TraceId = traceId;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset At { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorType { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string EntityType { get; private set; } = null!;
    public Guid? EntityId { get; private set; }
    public string? Before { get; private set; }
    public string? After { get; private set; }
    public string? Ip { get; private set; }
    public string? Ua { get; private set; }
    public string? TraceId { get; private set; }
}
