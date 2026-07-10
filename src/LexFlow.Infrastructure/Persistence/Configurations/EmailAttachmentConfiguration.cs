using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class EmailAttachmentConfiguration : IEntityTypeConfiguration<EmailAttachment>
{
    public void Configure(EntityTypeBuilder<EmailAttachment> builder)
    {
        builder.ToTable("email_attachments", "comm");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.MessageId).HasColumnName("message_id").IsRequired();
        builder.Property(a => a.MessageSentAt).HasColumnName("message_sent_at").IsRequired();
        builder.Property(a => a.DocumentId).HasColumnName("document_id");
        builder.Property(a => a.Filename).HasColumnName("filename").IsRequired();
        builder.Property(a => a.SizeBytes).HasColumnName("size_bytes");
        builder.Property(a => a.Mime).HasColumnName("mime");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
