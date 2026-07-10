using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>Maps column-for-column to comm.sms_messages (lexflow-database Scripts/08_Comm/SmsMessages). AC-CM4: dltTemplateId non-null proves DLT-compliant send in India-compliance mode.</summary>
public sealed class SmsMessage : AuditableEntity
{
    private SmsMessage()
    {
    }

    public SmsMessage(Guid tenantId, Guid? clientId, Guid? matterId, string direction, string? fromNumber, string? toNumber, string? body, string? dltTemplateId, string? provider)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ClientId = clientId;
        MatterId = matterId;
        Direction = direction;
        FromNumber = fromNumber;
        ToNumber = toNumber;
        Body = body;
        DltTemplateId = dltTemplateId;
        Provider = provider;
        Status = "Queued";
        SentAt = DateTimeOffset.UtcNow;
    }

    public Guid? ClientId { get; private set; }
    public Guid? MatterId { get; private set; }
    public string Direction { get; private set; } = null!;
    public string? FromNumber { get; private set; }
    public string? ToNumber { get; private set; }
    public string? Body { get; private set; }
    public string? DltTemplateId { get; private set; }
    public string? Provider { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string Status { get; private set; } = "Queued";
    public DateTimeOffset SentAt { get; private set; }

    public void SetProviderResult(string? providerMessageId, string status)
    {
        ProviderMessageId = providerMessageId;
        Status = status;
    }
}
