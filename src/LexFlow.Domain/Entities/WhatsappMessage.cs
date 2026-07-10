using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.whatsapp_messages (lexflow-database
/// Scripts/08_Comm/WhatsappMessages). WindowExpiresAt tracks the 24-hour session
/// window (Module 11: "session messages (24-h window) + approved template messages
/// (HSM)") — a session reply extends it, a template send doesn't need it open.
/// </summary>
public sealed class WhatsappMessage : AuditableEntity
{
    private WhatsappMessage()
    {
    }

    public WhatsappMessage(Guid tenantId, Guid? clientId, string waMsgId, string direction, Guid? templateId, string? body, Guid? mediaDocumentId, DateTimeOffset? windowExpiresAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        WaMsgId = waMsgId;
        Direction = direction;
        TemplateId = templateId;
        Body = body;
        MediaDocumentId = mediaDocumentId;
        Status = "Queued";
        WindowExpiresAt = windowExpiresAt;
    }

    public Guid? ClientId { get; private set; }
    public string WaMsgId { get; private set; } = null!;
    public string Direction { get; private set; } = null!;
    public Guid? TemplateId { get; private set; }
    public string? Body { get; private set; }
    public Guid? MediaDocumentId { get; private set; }
    public string Status { get; private set; } = "Queued";
    public DateTimeOffset? WindowExpiresAt { get; private set; }

    public void SetStatus(string status) => Status = status;
}
