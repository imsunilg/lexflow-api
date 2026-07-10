using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class CallLogConfiguration : IEntityTypeConfiguration<CallLog>
{
    public void Configure(EntityTypeBuilder<CallLog> builder)
    {
        builder.ToTable("call_logs", "comm");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.MatterId).HasColumnName("matter_id");
        builder.Property(c => c.UserId).HasColumnName("user_id");
        builder.Property(c => c.Direction).HasColumnName("direction").IsRequired();
        builder.Property(c => c.DurationSec).HasColumnName("duration_sec").IsRequired();
        builder.Property(c => c.Summary).HasColumnName("summary");
        builder.Property(c => c.FollowUpTaskId).HasColumnName("follow_up_task_id");
        builder.Property(c => c.RecordingBlobPath).HasColumnName("recording_blob_path");
        builder.Property(c => c.ConsentGiven).HasColumnName("consent_given").IsRequired();
        builder.Property(c => c.Provider).HasColumnName("provider");
        builder.Property(c => c.ProviderCallId).HasColumnName("provider_call_id");
        builder.Property(c => c.OccurredAt).HasColumnName("occurred_at").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
