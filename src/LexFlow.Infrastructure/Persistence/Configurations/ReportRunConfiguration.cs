using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ReportRunConfiguration : IEntityTypeConfiguration<ReportRun>
{
    public void Configure(EntityTypeBuilder<ReportRun> builder)
    {
        builder.ToTable("report_runs", "rpt");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.ReportKey).HasColumnName("report_key");
        builder.Property(r => r.ReportDefinitionId).HasColumnName("report_definition_id");
        builder.Property(r => r.ScheduleId).HasColumnName("schedule_id");
        builder.Property(r => r.ParamsJson).HasColumnName("params_json").HasColumnType("jsonb").IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").IsRequired();
        builder.Property(r => r.Format).HasColumnName("format");
        builder.Property(r => r.RowCount).HasColumnName("row_count");
        builder.Property(r => r.ResultBlobPath).HasColumnName("result_blob_path");
        builder.Property(r => r.ErrorMessage).HasColumnName("error_message");
        builder.Property(r => r.RequestedBy).HasColumnName("requested_by");
        builder.Property(r => r.RequestedAt).HasColumnName("requested_at").IsRequired();
        builder.Property(r => r.StartedAt).HasColumnName("started_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
