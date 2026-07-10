using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class CourtOrderConfiguration : IEntityTypeConfiguration<CourtOrder>
{
    public void Configure(EntityTypeBuilder<CourtOrder> builder)
    {
        builder.ToTable("court_orders", "legal");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(o => o.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(o => o.HearingId).HasColumnName("hearing_id");
        builder.Property(o => o.OrderDate).HasColumnName("order_date").IsRequired();
        builder.Property(o => o.Gist).HasColumnName("gist");
        builder.Property(o => o.ComplianceDue).HasColumnName("compliance_due");
        builder.Property(o => o.DocumentId).HasColumnName("document_id");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(o => o.DeletedAt).HasColumnName("deleted_at");
        builder.Property(o => o.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
