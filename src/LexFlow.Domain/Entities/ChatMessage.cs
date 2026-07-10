using LexFlow.Domain.Common;

namespace LexFlow.Domain.Entities;

/// <summary>
/// Maps column-for-column to comm.chat_messages (lexflow-database Scripts/08_Comm/ChatMessages).
/// <see cref="Seq"/> is a DB-generated identity column (globally monotonic across all
/// channels) — EF never sets it; it's populated on SaveChanges via ValueGeneratedOnAdd,
/// enabling stable keyset pagination (WHERE channel_id = ? AND seq &gt; ?).
/// </summary>
public sealed class ChatMessage : AuditableEntity
{
    private ChatMessage()
    {
    }

    public ChatMessage(Guid tenantId, Guid channelId, Guid? senderId, string? body, Guid? taskId, Guid? documentId)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ChannelId = channelId;
        SenderId = senderId;
        Body = body;
        TaskId = taskId;
        DocumentId = documentId;
        SentAt = DateTimeOffset.UtcNow;
    }

    public Guid ChannelId { get; private set; }
    public Guid? SenderId { get; private set; }
    public string? Body { get; private set; }
    public long Seq { get; private set; }
    public Guid? TaskId { get; private set; }
    public Guid? DocumentId { get; private set; }
    public DateTimeOffset SentAt { get; private set; }

    /// <summary>Module 11: "message -&gt; task conversion" — links the originating message to the task it was converted into.</summary>
    public void SetTaskId(Guid taskId) => TaskId = taskId;
}
