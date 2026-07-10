using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation.Results;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Settings;

/// <summary>Module 15 generic per-section settings store — see ISettingsService for the section-to-table dispatch rules.</summary>
public sealed class SettingsService(LexFlowDbContext db, IGatewayConfigService gatewayConfigService) : ISettingsService
{
    public async Task<string> GetSectionAsync(Guid tenantId, string section, CancellationToken cancellationToken = default)
    {
        if (SettingsSchemaProvider.GatewaySections.Contains(section))
        {
            return await GetGatewaySectionAsync(tenantId, section, cancellationToken);
        }

        if (section == "taxes")
        {
            var rows = await db.TaxConfigs.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);
            return JsonSerializer.Serialize(rows.Select(r => new { r.Id, r.CountryCode, r.TaxType, r.IsActive, Components = JsonNode.Parse(r.ComponentsJson) }));
        }

        if (section == "number_series")
        {
            var rows = await db.NumberSeries.Where(s => s.TenantId == tenantId).ToListAsync(cancellationToken);
            return JsonSerializer.Serialize(rows.Select(r => new { r.Id, r.SeriesKey, r.FiscalYear, r.FormatPattern, r.NextSeq }));
        }

        if (section == "email_templates")
        {
            var rows = await db.CommTemplates.Where(t => t.TenantId == tenantId).ToListAsync(cancellationToken);
            return JsonSerializer.Serialize(rows.Select(r => new { r.Id, r.Channel, r.Name, r.IsActive }));
        }

        if (section == "workflow_rules")
        {
            var rows = await db.WorkflowRules.Where(w => w.TenantId == tenantId).ToListAsync(cancellationToken);
            return JsonSerializer.Serialize(rows.Select(r => new { r.Id, r.Name, r.TriggerEvent, r.Active, r.RunOrder }));
        }

        // Blob sections (firm_details, branding, theme, document_templates,
        // business_hours, data, security) default to "{}" until first PUT.
        var setting = await db.TenantSettings.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Key == section, cancellationToken);
        return setting?.ValueJson ?? "{}";
    }

    public async Task<string> UpdateSectionAsync(Guid tenantId, Guid actorId, string section, string valueJson, CancellationToken cancellationToken = default)
    {
        if (SettingsSchemaProvider.CollectionSections.Contains(section))
        {
            throw new ConflictException(
                $"Section '{section}' is a collection — use its dedicated CRUD endpoint instead of PUT /settings/{section}.",
                "SETTINGS_SECTION_IS_COLLECTION");
        }

        if (SettingsSchemaProvider.GatewaySections.Contains(section))
        {
            // "secret" never appears in any schema's properties and is stripped here
            // before validation/storage — it must never reach config_json (PRD §20/
            // Module 15 Security: "secrets write-only, never returned").
            var (configJson, secret) = ExtractSecret(valueJson);
            ValidateAgainstSchema(section, configJson);
            return await PutGatewaySectionAsync(tenantId, section, configJson, secret, cancellationToken);
        }

        ValidateAgainstSchema(section, valueJson);

        var setting = await db.TenantSettings.SingleOrDefaultAsync(s => s.TenantId == tenantId && s.Key == section, cancellationToken);
        if (setting is null)
        {
            setting = new TenantSetting(tenantId, section, valueJson);
            await db.TenantSettings.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.SetValue(valueJson);
        }

        await db.SaveChangesAsync(cancellationToken);
        return setting.ValueJson;
    }

    public async Task<IReadOnlyList<SettingsAuditEntryDto>> GetAuditAsync(Guid tenantId, string? section, CancellationToken cancellationToken = default)
    {
        var rows = await db.AuditEvents
            .Where(e => e.TenantId == tenantId && (e.EntityType == "core.tenant_settings" || e.EntityType == "core.gateway_configs"))
            .OrderByDescending(e => e.At)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(section))
        {
            rows = rows.Where(e => PayloadMatchesSection(e.After ?? e.Before, section)).ToList();
        }

        return rows.Select(e => new SettingsAuditEntryDto(e.Id, e.At, e.ActorUserId, e.Action, e.Before, e.After)).ToList();
    }

    private async Task<string> GetGatewaySectionAsync(Guid tenantId, string section, CancellationToken cancellationToken)
    {
        var providers = SectionProviders(section);
        var configs = await db.GatewayConfigs
            .Where(g => g.TenantId == tenantId && providers.Contains(g.Provider))
            .ToListAsync(cancellationToken);

        var shaped = configs.Select(c => new
        {
            c.Provider,
            Config = JsonNode.Parse(c.ConfigJson),
            hasSecret = !string.IsNullOrEmpty(c.SecretKeyVaultRef),
            c.IsEnabled,
            c.IsTestMode,
        });

        // payment_gateways is genuinely multi-provider; the single-provider sections
        // (smtp/sms_gateway/whatsapp) unwrap to one object (or "{}" if never configured)
        // so GET/PUT round-trip the same shape the JSON schema above expects.
        return section == "payment_gateways"
            ? JsonSerializer.Serialize(shaped)
            : JsonSerializer.Serialize(shaped.FirstOrDefault()?.Config ?? JsonNode.Parse("{}"));
    }

    private async Task<string> PutGatewaySectionAsync(Guid tenantId, string section, string configJson, string? secret, CancellationToken cancellationToken)
    {
        var provider = SectionProviders(section)[0];

        // Preserve is_enabled/is_test_mode across a config-only save — this endpoint
        // doesn't expose those two flags; enabling a gateway happens via the dedicated
        // verify flow (POST /settings/gateways/{g}/verify), not a plain section PUT.
        var current = await gatewayConfigService.GetByProviderAsync(tenantId, provider, cancellationToken);
        var isEnabled = current?.IsEnabled ?? false;
        var isTestMode = current?.IsTestMode ?? true;

        var updated = await gatewayConfigService.UpsertAsync(tenantId, provider, configJson, secret, isEnabled, isTestMode, cancellationToken);
        return updated.ConfigJson;
    }

    private static (string ConfigJson, string? Secret) ExtractSecret(string valueJson)
    {
        var node = JsonNode.Parse(valueJson) as JsonObject ?? [];
        var secret = node["secret"]?.GetValue<string>();
        node.Remove("secret");
        return (node.ToJsonString(), secret);
    }

    private static string[] SectionProviders(string section) => section switch
    {
        "smtp" => ["smtp"],
        "sms_gateway" => ["sms_twilio", "sms_msg91"],
        "whatsapp" => ["whatsapp"],
        "payment_gateways" => ["stripe", "razorpay", "paypal"],
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "Not a gateway-backed section."),
    };

    private static void ValidateAgainstSchema(string section, string valueJson)
    {
        if (!SettingsSchemaProvider.TryGetSchema(section, out var schema))
        {
            throw new NotFoundException("SettingsSection", section);
        }

        JsonNode? instance;
        try
        {
            instance = JsonNode.Parse(valueJson);
        }
        catch (JsonException)
        {
            throw new Application.Common.Exceptions.ValidationException([new ValidationFailure("valueJson", "Value must be valid JSON.")]);
        }

        var result = schema.Evaluate(instance);
        if (!result.IsValid)
        {
            var errors = (result.Details ?? [])
                .Where(d => d.HasErrors)
                .SelectMany(d => d.Errors!.Select(kv => new ValidationFailure(d.InstanceLocation.ToString(), $"{kv.Key}: {kv.Value}")))
                .ToList();

            if (errors.Count == 0)
            {
                errors.Add(new ValidationFailure(section, "Value does not match the section's JSON schema."));
            }

            throw new Application.Common.Exceptions.ValidationException(errors);
        }
    }

    private static bool PayloadMatchesSection(string? payloadJson, string section)
    {
        if (string.IsNullOrEmpty(payloadJson))
        {
            return false;
        }

        try
        {
            // Property names here are the CLR property names AuditSaveChangesInterceptor
            // serializes (e.g. "Key", "Provider"), not the snake_case DB column names.
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.TryGetProperty("Key", out var key) && key.GetString() == section)
            {
                return true;
            }

            if (document.RootElement.TryGetProperty("Provider", out var provider) && provider.ValueKind == JsonValueKind.String)
            {
                return SettingsSchemaProvider.GatewaySections.Contains(section) && SectionProviders(section).Contains(provider.GetString());
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
