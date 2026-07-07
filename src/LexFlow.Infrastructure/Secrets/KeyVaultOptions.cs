namespace LexFlow.Infrastructure.Secrets;

/// <summary>Bound from configuration section "KeyVault". Per PRD §20(7): secrets live only in Key Vault, never in config/env.</summary>
public sealed class KeyVaultOptions
{
    public const string SectionName = "KeyVault";

    public string? VaultUri { get; set; }
}
