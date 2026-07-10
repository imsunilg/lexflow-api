using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RptFactMattersConfiguration : IEntityTypeConfiguration<RptFactMatters>
{
    public void Configure(EntityTypeBuilder<RptFactMatters> builder)
    {
        builder.ToTable("rpt_fact_matters", "rpt");
        builder.HasKey(f => f.FactMatterKey);

        builder.Property(f => f.FactMatterKey).HasColumnName("fact_matter_key");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(f => f.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(f => f.OpenedDateKey).HasColumnName("opened_date_key");
        builder.Property(f => f.ClosedDateKey).HasColumnName("closed_date_key");
        builder.Property(f => f.LawyerKey).HasColumnName("lawyer_key");
        builder.Property(f => f.ClientKey).HasColumnName("client_key");
        builder.Property(f => f.PracticeAreaKey).HasColumnName("practice_area_key");
        builder.Property(f => f.BranchId).HasColumnName("branch_id");
        builder.Property(f => f.Status).HasColumnName("status");
        builder.Property(f => f.Outcome).HasColumnName("outcome");
        builder.Property(f => f.CycleTimeDays).HasColumnName("cycle_time_days");
        builder.Property(f => f.IsOpen).HasColumnName("is_open").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.MatterId }).IsUnique();
    }
}
