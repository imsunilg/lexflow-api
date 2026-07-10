namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 15 generic per-section settings store (PRD §17: GET/PUT /api/v1/settings/{section}).
/// Dispatches by section name to whichever table actually backs it:
/// - 7 sections with no dedicated typed table (firm_details, branding, theme,
///   document_templates, business_hours, data, security) live in core.tenant_settings
///   as a validated JSON blob.
/// - smtp, sms_gateway, whatsapp, payment_gateways read/write core.gateway_configs
///   (non-secret fields only — see IGatewayConfigService for secret handling).
/// - taxes, number_series, email_templates, workflow_rules are collection-shaped and
///   therefore read-only through this generic endpoint; PUT is rejected with a message
///   pointing at the dedicated CRUD endpoints (ITaxRateService/INumberSeriesService/
///   ICommTemplateService — workflow_rules CRUD is out of scope of this prompt, §23's
///   own builder UI owns it).
/// </summary>
public interface ISettingsService
{
    Task<string> GetSectionAsync(Guid tenantId, string section, CancellationToken cancellationToken = default);

    /// <summary>Validates <paramref name="valueJson"/> against the section's JSON schema before persisting (PRD Module 15 Validation).</summary>
    Task<string> UpdateSectionAsync(Guid tenantId, Guid actorId, string section, string valueJson, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettingsAuditEntryDto>> GetAuditAsync(Guid tenantId, string? section, CancellationToken cancellationToken = default);
}

public sealed record SettingsAuditEntryDto(Guid Id, DateTimeOffset At, Guid? ActorUserId, string Action, string? Before, string? After);
