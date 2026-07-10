using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class EmailThreadConfiguration : IEntityTypeConfiguration<EmailThread>
{
    public void Configure(EntityTypeBuilder<EmailThread> builder)
    {
        builder.ToTable("email_threads", "comm");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.Subject).HasColumnName("subject");
        builder.Property(t => t.MatterId).HasColumnName("matter_id");
        builder.Property(t => t.ClientId).HasColumnName("client_id");
        builder.Property(t => t.MailboxId).HasColumnName("mailbox_id");
        builder.Property(t => t.LastMessageAt).HasColumnName("last_message_at");

        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(t => t.DeletedAt).HasColumnName("deleted_at");
        builder.Property(t => t.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
