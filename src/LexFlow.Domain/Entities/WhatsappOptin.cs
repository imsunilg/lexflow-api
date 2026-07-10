using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.whatsapp_optins (lexflow-database
/// Scripts/08_Comm/WhatsappOptins). Module 11: "opt-in tracked per client (mandatory
/// before first template send)"; edge case: "WhatsApp number changes (history
/// preserved, new opt-in required)" — a phone-number change is a new row, not an
/// update of this one.
/// </summary>
public sealed class WhatsappOptin : AuditableEntity
{
    private WhatsappOptin()
    {
    }

    public WhatsappOptin(Guid tenantId, Guid clientId, string phoneE164, string? source)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        PhoneE164 = phoneE164;
        Source = source;
        OptedInAt = DateTimeOffset.UtcNow;
    }

    public Guid ClientId { get; private set; }
    public string PhoneE164 { get; private set; } = null!;
    public DateTimeOffset OptedInAt { get; private set; }
    public DateTimeOffset? OptedOutAt { get; private set; }
    public string? Source { get; private set; }

    public void OptOut() => OptedOutAt ??= DateTimeOffset.UtcNow;
}
