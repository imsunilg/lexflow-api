using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ChatChannelConfiguration : IEntityTypeConfiguration<ChatChannel>
{
    public void Configure(EntityTypeBuilder<ChatChannel> builder)
    {
        builder.ToTable("chat_channels", "comm");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.Kind).HasColumnName("kind").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name");
        builder.Property(c => c.MatterId).HasColumnName("matter_id");
        builder.Property(c => c.TeamId).HasColumnName("team_id");
        builder.Property(c => c.RetentionDays).HasColumnName("retention_days");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
