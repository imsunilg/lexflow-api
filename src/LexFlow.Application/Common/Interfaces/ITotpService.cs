namespace LexFlow.Application.Common.Interfaces;

/// <summary>RFC 6238 TOTP for 2FA enrollment/verification (PRD §20(2)).</summary>
public interface ITotpService
{
    byte[] GenerateSecret();

    string BuildProvisioningUri(byte[] secret, string accountEmail, string issuer = "LexFlow");

    bool ValidateCode(byte[] secret, string code, int windowSteps = 1);

    IReadOnlyList<string> GenerateRecoveryCodes(int count = 10);
}
