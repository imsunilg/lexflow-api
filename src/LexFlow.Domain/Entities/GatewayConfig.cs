using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.gateway_configs (lexflow-database
/// Scripts/02_Core/GatewayConfigs). One row per (tenant, provider); secrets are never
/// stored here — <see cref="SecretKeyVaultRef"/> only names the Key Vault entry (PRD §20,
/// Module 15 Security: "secrets write-only, never returned").
/// </summary>
public sealed class GatewayConfig : AuditableEntity
{
    private GatewayConfig()
    {
    }

    public GatewayConfig(Guid tenantId, string provider, string configJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Provider = provider;
        ConfigJson = configJson;
        IsEnabled = false;
        IsTestMode = true;
    }

    public string Provider { get; private set; } = null!;
    public string ConfigJson { get; private set; } = "{}";
    public string? SecretKeyVaultRef { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool IsTestMode { get; private set; }

    public void SetConfig(string configJson) => ConfigJson = configJson;

    public void SetSecretRef(string? secretKeyVaultRef) => SecretKeyVaultRef = secretKeyVaultRef;

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    public void SetTestMode(bool isTestMode) => IsTestMode = isTestMode;
}
