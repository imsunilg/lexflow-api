using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RptDimLawyerConfiguration : IEntityTypeConfiguration<RptDimLawyer>
{
    public void Configure(EntityTypeBuilder<RptDimLawyer> builder)
    {
        builder.ToTable("rpt_dim_lawyer", "rpt");
        builder.HasKey(l => l.LawyerKey);

        builder.Property(l => l.LawyerKey).HasColumnName("lawyer_key");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.Name).HasColumnName("name");
        builder.Property(l => l.RoleKey).HasColumnName("role_key");
        builder.Property(l => l.BranchId).HasColumnName("branch_id");
        builder.Property(l => l.DepartmentId).HasColumnName("department_id");
        builder.Property(l => l.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
