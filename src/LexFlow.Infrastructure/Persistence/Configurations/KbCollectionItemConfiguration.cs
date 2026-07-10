using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class KbCollectionItemConfiguration : IEntityTypeConfiguration<KbCollectionItem>
{
    public void Configure(EntityTypeBuilder<KbCollectionItem> builder)
    {
        builder.ToTable("kb_collection_items", "kb");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(i => i.CollectionId).HasColumnName("collection_id").IsRequired();
        builder.Property(i => i.KbRefKind).HasColumnName("kb_ref_kind").IsRequired();
        builder.Property(i => i.KbRefId).HasColumnName("kb_ref_id").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.Property(i => i.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(i => i.DeletedAt).HasColumnName("deleted_at");
        builder.Property(i => i.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
