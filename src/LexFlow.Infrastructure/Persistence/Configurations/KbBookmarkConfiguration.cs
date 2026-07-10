using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class KbBookmarkConfiguration : IEntityTypeConfiguration<KbBookmark>
{
    public void Configure(EntityTypeBuilder<KbBookmark> builder)
    {
        builder.ToTable("kb_bookmarks", "kb");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(b => b.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(b => b.KbRefKind).HasColumnName("kb_ref_kind").IsRequired();
        builder.Property(b => b.KbRefId).HasColumnName("kb_ref_id").IsRequired();

        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.CreatedBy).HasColumnName("created_by");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.UpdatedBy).HasColumnName("updated_by");
        builder.Property(b => b.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(b => b.DeletedAt).HasColumnName("deleted_at");
        builder.Property(b => b.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(b => !b.IsDeleted);
    }
}
