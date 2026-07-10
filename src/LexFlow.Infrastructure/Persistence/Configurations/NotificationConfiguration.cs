using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", "ops");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(n => n.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(n => n.Kind).HasColumnName("kind").IsRequired();
        builder.Property(n => n.Title).HasColumnName("title").IsRequired();
        builder.Property(n => n.Body).HasColumnName("body");
        builder.Property(n => n.DeepLink).HasColumnName("deep_link");
        builder.Property(n => n.ReadAt).HasColumnName("read_at");
        builder.Property(n => n.ChannelsJson).HasColumnName("channels").HasColumnType("jsonb").IsRequired();

        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(n => n.CreatedBy).HasColumnName("created_by");
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at");
        builder.Property(n => n.UpdatedBy).HasColumnName("updated_by");
        builder.Property(n => n.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(n => n.DeletedAt).HasColumnName("deleted_at");
        builder.Property(n => n.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(n => !n.IsDeleted);
    }
}
