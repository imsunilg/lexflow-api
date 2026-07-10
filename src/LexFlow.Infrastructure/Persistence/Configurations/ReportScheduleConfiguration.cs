using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ReportScheduleConfiguration : IEntityTypeConfiguration<ReportSchedule>
{
    public void Configure(EntityTypeBuilder<ReportSchedule> builder)
    {
        builder.ToTable("report_schedules", "rpt");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.ReportKey).HasColumnName("report_key");
        builder.Property(s => s.ReportDefinitionId).HasColumnName("report_definition_id");
        builder.Property(s => s.Frequency).HasColumnName("frequency").IsRequired();
        builder.Property(s => s.Format).HasColumnName("format").IsRequired();
        builder.Property(s => s.ParamsJson).HasColumnName("params_json").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.RecipientsJson).HasColumnName("recipients_json").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.NextRunAt).HasColumnName("next_run_at");
        builder.Property(s => s.LastRunAt).HasColumnName("last_run_at");
        builder.Property(s => s.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
