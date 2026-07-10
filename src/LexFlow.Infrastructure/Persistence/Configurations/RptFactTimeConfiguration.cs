using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class RptFactTimeConfiguration : IEntityTypeConfiguration<RptFactTime>
{
    public void Configure(EntityTypeBuilder<RptFactTime> builder)
    {
        builder.ToTable("rpt_fact_time", "rpt");
        builder.HasKey(f => f.FactTimeKey);

        builder.Property(f => f.FactTimeKey).HasColumnName("fact_time_key");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(f => f.TimeEntryId).HasColumnName("time_entry_id").IsRequired();
        builder.Property(f => f.DateKey).HasColumnName("date_key");
        builder.Property(f => f.LawyerKey).HasColumnName("lawyer_key");
        builder.Property(f => f.ClientKey).HasColumnName("client_key");
        builder.Property(f => f.PracticeAreaKey).HasColumnName("practice_area_key");
        builder.Property(f => f.BranchId).HasColumnName("branch_id");
        builder.Property(f => f.MatterId).HasColumnName("matter_id");
        builder.Property(f => f.DurationMin).HasColumnName("duration_min");
        builder.Property(f => f.RoundedMin).HasColumnName("rounded_min");
        builder.Property(f => f.Billable).HasColumnName("billable").IsRequired();
        builder.Property(f => f.IsBilled).HasColumnName("is_billed").IsRequired();
        builder.Property(f => f.Amount).HasColumnName("amount").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.TimeEntryId }).IsUnique();
    }
}
