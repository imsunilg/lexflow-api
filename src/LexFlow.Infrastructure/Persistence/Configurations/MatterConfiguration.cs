using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class MatterConfiguration : IEntityTypeConfiguration<Matter>
{
    public void Configure(EntityTypeBuilder<Matter> builder)
    {
        builder.ToTable("matters", "legal");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.Number).HasColumnName("number").IsRequired();
        builder.Property(m => m.Title).HasColumnName("title").IsRequired();
        builder.Property(m => m.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(m => m.MatterType).HasColumnName("matter_type").IsRequired();
        builder.Property(m => m.PracticeAreaId).HasColumnName("practice_area_id");
        builder.Property(m => m.BranchId).HasColumnName("branch_id");
        builder.Property(m => m.ResponsibleLawyerId).HasColumnName("responsible_lawyer_id");
        builder.Property(m => m.Priority).HasColumnName("priority").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").IsRequired();
        builder.Property(m => m.Outcome).HasColumnName("outcome");
        builder.Property(m => m.OpenedOn).HasColumnName("opened_on").IsRequired();
        builder.Property(m => m.ClosedOn).HasColumnName("closed_on");
        builder.Property(m => m.IsPrivate).HasColumnName("is_private").IsRequired();
        builder.Property(m => m.Budget).HasColumnName("budget").HasColumnType("numeric(18,2)");
        builder.Property(m => m.Description).HasColumnName("description");
        builder.Property(m => m.BillingArrangementJson).HasColumnName("billing_arrangement").HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.AiAllowed).HasColumnName("ai_allowed").IsRequired();

        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");
        builder.Property(m => m.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
