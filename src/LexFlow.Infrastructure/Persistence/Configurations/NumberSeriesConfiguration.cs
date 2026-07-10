using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class NumberSeriesConfiguration : IEntityTypeConfiguration<NumberSeries>
{
    public void Configure(EntityTypeBuilder<NumberSeries> builder)
    {
        builder.ToTable("number_series", "fin");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id).HasColumnName("id");
        builder.Property(n => n.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(n => n.BranchId).HasColumnName("branch_id");
        builder.Property(n => n.SeriesKey).HasColumnName("series_key").IsRequired();
        builder.Property(n => n.FiscalYear).HasColumnName("fiscal_year").IsRequired();
        builder.Property(n => n.FormatPattern).HasColumnName("format_pattern").IsRequired();
        builder.Property(n => n.NextSeq).HasColumnName("next_seq").IsRequired();

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
