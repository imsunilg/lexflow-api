using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class PortalMessageConfiguration : IEntityTypeConfiguration<PortalMessage>
{
    public void Configure(EntityTypeBuilder<PortalMessage> builder)
    {
        builder.ToTable("portal_messages", "portal");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.ThreadId).HasColumnName("thread_id").IsRequired();
        builder.Property(m => m.SenderClientPortalUserId).HasColumnName("sender_client_portal_user_id");
        builder.Property(m => m.SenderStaffUserId).HasColumnName("sender_staff_user_id");
        builder.Property(m => m.Body).HasColumnName("body").IsRequired();
        builder.Property(m => m.ReadAt).HasColumnName("read_at");

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
