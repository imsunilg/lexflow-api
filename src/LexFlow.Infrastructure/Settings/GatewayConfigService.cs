using Azure.Security.KeyVault.Secrets;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Settings;

/// <summary>
/// Module 15 §4-7 gateway config persistence. Secrets are pushed to Key Vault
/// (PRD §20 "Secrets: Key Vault only") when a SecretClient is configured; falls back
/// to a locally-synthesized reference name (never the secret value) when it isn't —
/// e.g. local dev without a Key Vault — same conditional-registration pattern already
/// used for KeyVaultOptions/SecretClient in DependencyInjection.
/// </summary>
public sealed class GatewayConfigService(LexFlowDbContext db, SecretClient? secretClient = null) : IGatewayConfigService
{
    public async Task<IReadOnlyList<GatewayConfigDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var configs = await db.GatewayConfigs.Where(g => g.TenantId == tenantId).ToListAsync(cancellationToken);
        return configs.Select(ToDto).ToList();
    }

    public async Task<GatewayConfigDto?> GetByProviderAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default)
    {
        var config = await db.GatewayConfigs.SingleOrDefaultAsync(g => g.TenantId == tenantId && g.Provider == provider, cancellationToken);
        return config is null ? null : ToDto(config);
    }

    public async Task<GatewayConfigDto> UpsertAsync(Guid tenantId, string provider, string configJson, string? secret, bool isEnabled, bool isTestMode, CancellationToken cancellationToken = default)
    {
        var gatewayConfig = await db.GatewayConfigs.SingleOrDefaultAsync(g => g.TenantId == tenantId && g.Provider == provider, cancellationToken);

        if (gatewayConfig is null)
        {
            gatewayConfig = new GatewayConfig(tenantId, provider, configJson);
            await db.GatewayConfigs.AddAsync(gatewayConfig, cancellationToken);
        }
        else
        {
            gatewayConfig.SetConfig(configJson);
        }

        gatewayConfig.SetEnabled(isEnabled);
        gatewayConfig.SetTestMode(isTestMode);

        if (!string.IsNullOrWhiteSpace(secret))
        {
            var secretName = $"gw-{tenantId:N}-{provider}";
            if (secretClient is not null)
            {
                await secretClient.SetSecretAsync(secretName, secret, cancellationToken);
            }

            gatewayConfig.SetSecretRef(secretName);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(gatewayConfig);
    }

    private static GatewayConfigDto ToDto(GatewayConfig config) => new(
        config.Id,
        config.Provider,
        config.ConfigJson,
        !string.IsNullOrEmpty(config.SecretKeyVaultRef),
        config.IsEnabled,
        config.IsTestMode);
}
