using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

/// <summary>comm.email_messages is PARTITION BY RANGE(sent_at) with PK (id, sent_at) — same shape as ReminderDispatchLogConfiguration.</summary>
public sealed class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.ToTable("email_messages", "comm");
        builder.HasKey(m => new { m.Id, m.SentAt });

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.ThreadId).HasColumnName("thread_id");
        builder.Property(m => m.MessageIdHdr).HasColumnName("message_id_hdr").IsRequired();
        builder.Property(m => m.InReplyTo).HasColumnName("in_reply_to");
        builder.Property(m => m.Direction).HasColumnName("direction").IsRequired();
        builder.Property(m => m.FromAddr).HasColumnName("from_addr");
        builder.Property(m => m.ToAddrsJson).HasColumnName("to_addrs").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.Subject).HasColumnName("subject");
        builder.Property(m => m.BodyHtmlSanitized).HasColumnName("body_html_sanitized");
        builder.Property(m => m.HasAttachments).HasColumnName("has_attachments").IsRequired();
        builder.Property(m => m.SentAt).HasColumnName("sent_at").IsRequired();
        builder.Property(m => m.MatterId).HasColumnName("matter_id");
        builder.Property(m => m.ClientId).HasColumnName("client_id");

        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");
        builder.Property(m => m.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
