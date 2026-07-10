using System.Security.Cryptography;
using System.Text;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// RFC 6238 TOTP (HMAC-SHA1, 30 s step, 6 digits) per PRD §20(2). Implemented directly
/// against System.Security.Cryptography rather than a third-party TOTP package —
/// the algorithm is small and this avoids an extra dependency for ~40 lines of math.
/// </summary>
public sealed class TotpService : ITotpService
{
    private const int SecretSizeBytes = 20;
    private const int StepSeconds = 30;
    private const int Digits = 6;

    public byte[] GenerateSecret() => RandomNumberGenerator.GetBytes(SecretSizeBytes);

    public string BuildProvisioningUri(byte[] secret, string accountEmail, string issuer = "LexFlow")
    {
        var base32Secret = Base32Encode(secret);
        var label = Uri.EscapeDataString($"{issuer}:{accountEmail}");
        var encodedIssuer = Uri.EscapeDataString(issuer);
        return $"otpauth://totp/{label}?secret={base32Secret}&issuer={encodedIssuer}&digits={Digits}&period={StepSeconds}&algorithm=SHA1";
    }

    public bool ValidateCode(byte[] secret, string code, int windowSteps = 1)
    {
        if (code.Length != Digits || !code.All(char.IsDigit))
        {
            return false;
        }

        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / StepSeconds;

        for (var window = -windowSteps; window <= windowSteps; window++)
        {
            var candidate = ComputeCode(secret, currentStep + window);
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(candidate),
                    Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    public IReadOnlyList<string> GenerateRecoveryCodes(int count = 10)
    {
        var codes = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var bytes = RandomNumberGenerator.GetBytes(5);
            codes.Add(Convert.ToHexString(bytes).ToLowerInvariant());
        }

        return codes;
    }

    private static string ComputeCode(byte[] secret, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(counterBytes);
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes);

        var offset = hash[^1] & 0x0F;
        var binaryCode = ((hash[offset] & 0x7F) << 24)
                          | ((hash[offset + 1] & 0xFF) << 16)
                          | ((hash[offset + 2] & 0xFF) << 8)
                          | (hash[offset + 3] & 0xFF);

        var truncated = binaryCode % (int)Math.Pow(10, Digits);
        return truncated.ToString().PadLeft(Digits, '0');
    }

    private static string Base32Encode(byte[] data)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var builder = new StringBuilder();
        int bitBuffer = 0, bitCount = 0;

        foreach (var b in data)
        {
            bitBuffer = (bitBuffer << 8) | b;
            bitCount += 8;
            while (bitCount >= 5)
            {
                bitCount -= 5;
                builder.Append(alphabet[(bitBuffer >> bitCount) & 0x1F]);
            }
        }

        if (bitCount > 0)
        {
            builder.Append(alphabet[(bitBuffer << (5 - bitCount)) & 0x1F]);
        }

        return builder.ToString();
    }
}
