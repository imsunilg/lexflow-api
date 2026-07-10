using System.Data;
using System.Data.Common;
using Azure.Security.KeyVault.Secrets;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LexFlow.Infrastructure.Crm;

/// <summary>
/// Module 3 KYC field-level encryption. Calls Postgres's pgcrypto functions
/// (pgp_sym_encrypt/pgp_sym_decrypt, enabled in 00_Extensions per DB-1) directly over
/// the DbContext's ADO.NET connection — the encryption itself happens inside Postgres,
/// but the passphrase used to key it is sourced from Key Vault by this Infrastructure
/// service (falling back to a local dev passphrase when no Key Vault is configured,
/// same conditional-registration pattern as GatewayConfigService), so a database-only
/// compromise alone cannot decrypt existing rows.
/// </summary>
public sealed class KycEncryptionService(LexFlowDbContext db, IConfiguration configuration, SecretClient? secretClient = null) : IKycEncryptionService
{
    private const string SecretName = "kyc-encryption-key";
    private string? _cachedKey;

    public async Task<byte[]> EncryptAsync(string plaintext, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyAsync(cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pgp_sym_encrypt(@plaintext, @key)";
        AddParameter(command, "plaintext", plaintext);
        AddParameter(command, "key", key);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (byte[])result!;
    }

    public async Task<string> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default)
    {
        var key = await GetKeyAsync(cancellationToken);
        var connection = await OpenConnectionAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pgp_sym_decrypt(@ciphertext, @key)";
        AddParameter(command, "ciphertext", ciphertext);
        AddParameter(command, "key", key);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (string)result!;
    }

    private async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        return connection;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<string> GetKeyAsync(CancellationToken cancellationToken)
    {
        if (_cachedKey is not null)
        {
            return _cachedKey;
        }

        if (secretClient is not null)
        {
            var secret = await secretClient.GetSecretAsync(SecretName, cancellationToken: cancellationToken);
            _cachedKey = secret.Value.Value;
            return _cachedKey;
        }

        _cachedKey = configuration["Kyc:LocalEncryptionKey"] ?? "lexflow-dev-only-kyc-key-do-not-use-in-production";
        return _cachedKey;
    }
}
