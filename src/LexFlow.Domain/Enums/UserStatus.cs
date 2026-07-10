namespace LexFlow.Domain.Enums;

/// <summary>Mirrors the ck_users_status CHECK constraint on core.users (PRD Module 14).</summary>
public enum UserStatus
{
    Invited,
    Active,
    Suspended,
    Deactivated
}
