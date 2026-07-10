using System.Security.Cryptography;
using Konscious.Security.Cryptography;
using LexFlow.Application.Common.Interfaces;

namespace LexFlow.Infrastructure.Security;

/// <summary>
/// Argon2id per PRD §20(2): m=64MB, t=3, p=4. Stores salt + parameters alongside the
/// hash (PHC-like format) so verification never depends on external configuration state.
/// </summary>
public sealed class Argon2PasswordHasher : IPasswordHasher
{
    private const int MemoryKib = 64 * 1024;
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = ComputeHash(password, salt, MemoryKib, Iterations, Parallelism, HashSize);

        return string.Join(
            '$',
            "argon2id",
            $"m={MemoryKib},t={Iterations},p={Parallelism}",
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length != 4 || parts[0] != "argon2id")
        {
            return false;
        }

        var parameters = parts[1].Split(',')
            .Select(p => p.Split('='))
            .ToDictionary(p => p[0], p => int.Parse(p[1]));

        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);

        var actual = ComputeHash(
            password,
            salt,
            parameters["m"],
            parameters["t"],
            parameters["p"],
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] ComputeHash(string password, byte[] salt, int memoryKib, int iterations, int parallelism, int hashSize)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism,
        };

        return argon2.GetBytes(hashSize);
    }
}
