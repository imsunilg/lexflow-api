using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches", "core");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(b => b.Name).HasColumnName("name").IsRequired();
        builder.Property(b => b.Code).HasColumnName("code").IsRequired();
        builder.Property(b => b.Address).HasColumnName("address").HasColumnType("jsonb").IsRequired();
        builder.Property(b => b.Tz).HasColumnName("tz").IsRequired();
        builder.Property(b => b.Gstin).HasColumnName("gstin");
        builder.Property(b => b.SeriesPrefix).HasColumnName("series_prefix");

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
