using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RptDimPracticeAreaConfiguration : IEntityTypeConfiguration<RptDimPracticeArea>
{
    public void Configure(EntityTypeBuilder<RptDimPracticeArea> builder)
    {
        builder.ToTable("rpt_dim_practice_area", "rpt");
        builder.HasKey(p => p.PracticeAreaKey);

        builder.Property(p => p.PracticeAreaKey).HasColumnName("practice_area_key");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name");
        builder.Property(p => p.ParentKey).HasColumnName("parent_key");
        builder.Property(p => p.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
