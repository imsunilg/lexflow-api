using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.ToTable("leads", "crm");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(l => l.Number).HasColumnName("number").IsRequired();
        builder.Property(l => l.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(l => l.LastName).HasColumnName("last_name");
        builder.Property(l => l.Company).HasColumnName("company");
        builder.Property(l => l.Email).HasColumnName("email");
        builder.Property(l => l.PhoneE164).HasColumnName("phone_e164");
        builder.Property(l => l.SourceId).HasColumnName("source_id");
        builder.Property(l => l.Stage).HasColumnName("stage").IsRequired();
        builder.Property(l => l.OwnerId).HasColumnName("owner_id");
        builder.Property(l => l.BranchId).HasColumnName("branch_id");
        builder.Property(l => l.PracticeAreaId).HasColumnName("practice_area_id");
        builder.Property(l => l.Score).HasColumnName("score").IsRequired();
        builder.Property(l => l.IssueSummary).HasColumnName("issue_summary");
        builder.Property(l => l.OpposingParty).HasColumnName("opposing_party");
        builder.Property(l => l.BudgetBand).HasColumnName("budget_band");
        builder.Property(l => l.Status).HasColumnName("status").IsRequired();
        builder.Property(l => l.LostReasonId).HasColumnName("lost_reason_id");
        builder.Property(l => l.ConvertedClientId).HasColumnName("converted_client_id");
        builder.Property(l => l.SlaFirstContactDue).HasColumnName("sla_first_contact_due");
        builder.Property(l => l.FirstContactedAt).HasColumnName("first_contacted_at");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(l => l.DeletedAt).HasColumnName("deleted_at");
        builder.Property(l => l.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
