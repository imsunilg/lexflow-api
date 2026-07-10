using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ChatMemberConfiguration : IEntityTypeConfiguration<ChatMember>
{
    public void Configure(EntityTypeBuilder<ChatMember> builder)
    {
        builder.ToTable("chat_members", "comm");
        builder.HasKey(m => new { m.ChannelId, m.UserId });

        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.ChannelId).HasColumnName("channel_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.Property(m => m.Role).HasColumnName("role").IsRequired();
        builder.Property(m => m.JoinedAt).HasColumnName("joined_at").IsRequired();

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
