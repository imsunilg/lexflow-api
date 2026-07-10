using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to core.login_history (lexflow-database Scripts/02_Core/LoginHistory).</summary>
public sealed class LoginHistory : AuditableEntity
{
    private LoginHistory()
    {
    }

    public LoginHistory(Guid tenantId, Guid? userId, string result, string? ip, string? ua, string? failureReason = null)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        UserId = userId;
        At = DateTimeOffset.UtcNow;
        Result = result;
        Ip = ip;
        Ua = ua;
        FailureReason = failureReason;
    }

    public Guid? UserId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public string? Ip { get; private set; }
    public string? Ua { get; private set; }
    public string Result { get; private set; } = null!;
    public string? FailureReason { get; private set; }
}
