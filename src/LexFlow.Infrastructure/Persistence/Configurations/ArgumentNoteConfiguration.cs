using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ArgumentNoteConfiguration : IEntityTypeConfiguration<ArgumentNote>
{
    public void Configure(EntityTypeBuilder<ArgumentNote> builder)
    {
        builder.ToTable("argument_notes", "legal");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(a => a.HearingId).HasColumnName("hearing_id");
        builder.Property(a => a.Stage).HasColumnName("stage");
        builder.Property(a => a.Body).HasColumnName("body").IsRequired();
        builder.Property(a => a.CitationJudgmentIds).HasColumnName("citation_judgment_ids");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
