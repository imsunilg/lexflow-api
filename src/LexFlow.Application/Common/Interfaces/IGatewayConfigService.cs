namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 15 §4-7 (SMTP, SMS Gateway, WhatsApp API, Payment Gateways) non-secret
/// config + secret-reference management. Secrets are write-only (PRD Module 15
/// Security: "secrets write-only, never returned") — <see cref="GatewayConfigDto"/>
/// exposes only whether a secret is currently set, never its value or Key Vault ref.
/// </summary>
public interface IGatewayConfigService
{
    Task<IReadOnlyList<GatewayConfigDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<GatewayConfigDto?> GetByProviderAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default);

    Task<GatewayConfigDto> UpsertAsync(Guid tenantId, string provider, string configJson, string? secret, bool isEnabled, bool isTestMode, CancellationToken cancellationToken = default);
}

public sealed record GatewayConfigDto(Guid Id, string Provider, string ConfigJson, bool HasSecret, bool IsEnabled, bool IsTestMode);
