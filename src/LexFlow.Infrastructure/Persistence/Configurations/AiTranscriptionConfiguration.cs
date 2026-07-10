using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class AiTranscriptionConfiguration : IEntityTypeConfiguration<AiTranscription>
{
    public void Configure(EntityTypeBuilder<AiTranscription> builder)
    {
        builder.ToTable("ai_transcriptions", "ai");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(t => t.MatterId).HasColumnName("matter_id");
        builder.Property(t => t.RequestedBy).HasColumnName("requested_by");
        builder.Property(t => t.Status).HasColumnName("status").IsRequired();
        builder.Property(t => t.BlobPath).HasColumnName("blob_path").IsRequired();
        builder.Property(t => t.DurationSec).HasColumnName("duration_sec");
        builder.Property(t => t.Language).HasColumnName("language");
        builder.Property(t => t.TranscriptText).HasColumnName("transcript_text");
        builder.Property(t => t.ErrorMessage).HasColumnName("error_message");
        builder.Property(t => t.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(t => t.CompletedAt).HasColumnName("completed_at");
    }
}
