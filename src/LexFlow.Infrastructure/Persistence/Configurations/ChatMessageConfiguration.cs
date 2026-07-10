using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages", "comm");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.ChannelId).HasColumnName("channel_id").IsRequired();
        builder.Property(m => m.SenderId).HasColumnName("sender_id");
        builder.Property(m => m.Body).HasColumnName("body");
        builder.Property(m => m.Seq).HasColumnName("seq").ValueGeneratedOnAdd();
        builder.Property(m => m.TaskId).HasColumnName("task_id");
        builder.Property(m => m.DocumentId).HasColumnName("document_id");
        builder.Property(m => m.SentAt).HasColumnName("sent_at").IsRequired();

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
