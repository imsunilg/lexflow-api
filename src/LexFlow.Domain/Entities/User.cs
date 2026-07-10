using LexFlow.Domain.Enums;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to core.users (lexflow-database Scripts/02_Core/Users).
/// Deliberately does NOT derive from AuditableEntity: core.users has the standard
/// tenant_id + audit trio, but modelling it inline here keeps the many auth-specific
/// mutators (password/2FA/status) next to the fields they touch.
/// </summary>
public sealed class User : LexFlow.Domain.Common.AuditableEntity
{
    private User()
    {
    }

    public User(Guid tenantId, string email, string name, Guid? branchId = null, Guid? departmentId = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Email = email;
        Name = name;
        BranchId = branchId;
        DepartmentId = departmentId;
        Status = UserStatus.Invited;
        NotificationPrefs = "{}";
    }

    public string Email { get; private set; } = null!;
    public string? PasswordHash { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Designation { get; private set; }
    public string? BarEnrollmentNo { get; private set; }
    public string? Phone { get; private set; }
    public string? PhotoBlobPath { get; private set; }
    public string? SignatureBlobPath { get; private set; }
    public decimal? CostRate { get; private set; }
    public Guid? BranchId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public UserStatus Status { get; private set; }
    public string? Tz { get; private set; }
    public string? Locale { get; private set; }
    public byte[]? TwoFaSecret { get; private set; }
    public bool TwoFaEnabled { get; private set; }
    public string NotificationPrefs { get; private set; } = "{}";

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void UpdateProfile(
        string name,
        string? designation,
        string? barEnrollmentNo,
        string? phone,
        decimal? costRate,
        Guid? branchId,
        Guid? departmentId,
        string? tz,
        string? locale,
        string notificationPrefsJson)
    {
        Name = name;
        Designation = designation;
        BarEnrollmentNo = barEnrollmentNo;
        Phone = phone;
        CostRate = costRate;
        BranchId = branchId;
        DepartmentId = departmentId;
        Tz = tz;
        Locale = locale;
        NotificationPrefs = notificationPrefsJson;
    }

    public void Activate() => Status = UserStatus.Active;

    public void Suspend() => Status = UserStatus.Suspended;

    public void Deactivate() => Status = UserStatus.Deactivated;

    public void EnableTwoFactor(byte[] secret)
    {
        TwoFaSecret = secret;
        TwoFaEnabled = true;
    }

    public void DisableTwoFactor()
    {
        TwoFaSecret = null;
        TwoFaEnabled = false;
    }

    /// <summary>Stores a freshly-generated secret without enabling 2FA yet — enrollment isn't complete until <see cref="ConfirmTwoFactor"/>.</summary>
    public void SetPendingTwoFactorSecret(byte[] secret) => TwoFaSecret = secret;

    /// <summary>Completes enrollment after the client has proven possession of the secret with a valid code.</summary>
    public void ConfirmTwoFactor() => TwoFaEnabled = true;
}
