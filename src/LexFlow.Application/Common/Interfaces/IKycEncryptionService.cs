namespace LexFlow.Application.Common.Interfaces;

/// <summary>
/// Module 3 KYC field-level encryption over the pgcrypto-backed bytea columns from
/// DB-3 (crm.client_identity_documents.doc_number_enc, crm.clients.pan_enc). The
/// encryption/decryption itself runs as a Postgres pgp_sym_encrypt/pgp_sym_decrypt
/// call (pgcrypto extension) issued from this Infrastructure service — the passphrase
/// is sourced from Key Vault (falling back to a local dev key), never from the
/// database itself, so a DB-only compromise cannot decrypt existing rows.
/// </summary>
public interface IKycEncryptionService
{
    Task<byte[]> EncryptAsync(string plaintext, CancellationToken cancellationToken = default);

    Task<string> DecryptAsync(byte[] ciphertext, CancellationToken cancellationToken = default);
}
