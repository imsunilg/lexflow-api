namespace LexFlow.Application.Common.Interfaces;

/// <summary>Argon2id password hashing per PRD §20(2): m=64MB, t=3, p=4.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}
