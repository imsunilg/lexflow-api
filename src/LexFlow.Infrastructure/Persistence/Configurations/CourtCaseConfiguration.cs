using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class CourtCaseConfiguration : IEntityTypeConfiguration<CourtCase>
{
    public void Configure(EntityTypeBuilder<CourtCase> builder)
    {
        builder.ToTable("court_cases", "legal");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(c => c.CourtId).HasColumnName("court_id").IsRequired();
        builder.Property(c => c.CaseType).HasColumnName("case_type").IsRequired();
        builder.Property(c => c.CaseNumber).HasColumnName("case_number").IsRequired();
        builder.Property(c => c.CaseYear).HasColumnName("case_year").IsRequired();
        builder.Property(c => c.CnrNumber).HasColumnName("cnr_number");
        builder.Property(c => c.FilingDate).HasColumnName("filing_date");
        builder.Property(c => c.Stage).HasColumnName("stage");
        builder.Property(c => c.JudgeId).HasColumnName("judge_id");
        builder.Property(c => c.Courtroom).HasColumnName("courtroom");
        builder.Property(c => c.Status).HasColumnName("status").IsRequired();
        builder.Property(c => c.AppealOfCaseId).HasColumnName("appeal_of_case_id");

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
