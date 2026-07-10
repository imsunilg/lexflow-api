using Azure.Security.KeyVault.Secrets;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Comm;

/// <summary>
/// Shared helper for comm providers (SMS/WhatsApp/Voice) to read a tenant's
/// core.gateway_configs row plus its Key Vault-backed secret — the read-side
/// counterpart of GatewayConfigService.UpsertAsync (Module 15), which writes the
/// secret but, by design (secrets are write-only through that service's own DTO),
/// never reads it back. Falls back to no secret when no SecretClient is configured
/// (local dev without Key Vault), same conditional pattern used throughout this
/// codebase (KycEncryptionService, GatewayConfigService itself).
/// </summary>
public sealed class GatewayCredentialResolver(LexFlowDbContext db, SecretClient? secretClient = null)
{
    public async Task<(GatewayConfig Config, string? Secret)?> ResolveAsync(Guid tenantId, string provider, CancellationToken cancellationToken = default)
    {
        var config = await db.GatewayConfigs.SingleOrDefaultAsync(g => g.TenantId == tenantId && g.Provider == provider && g.IsEnabled, cancellationToken);
        if (config is null)
        {
            return null;
        }

        string? secret = null;
        if (!string.IsNullOrEmpty(config.SecretKeyVaultRef) && secretClient is not null)
        {
            var response = await secretClient.GetSecretAsync(config.SecretKeyVaultRef, cancellationToken: cancellationToken);
            secret = response.Value.Value;
        }

        return (config, secret);
    }
}
