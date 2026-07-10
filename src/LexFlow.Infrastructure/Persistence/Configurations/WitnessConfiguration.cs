using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class WitnessConfiguration : IEntityTypeConfiguration<Witness>
{
    public void Configure(EntityTypeBuilder<Witness> builder)
    {
        builder.ToTable("witnesses", "legal");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(w => w.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(w => w.Name).HasColumnName("name").IsRequired();
        builder.Property(w => w.Side).HasColumnName("side");
        builder.Property(w => w.ContactJson).HasColumnName("contact").HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.ExamStatus).HasColumnName("exam_status").IsRequired();
        builder.Property(w => w.ScheduledOn).HasColumnName("scheduled_on");

        builder.Property(w => w.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(w => w.DeletedAt).HasColumnName("deleted_at");
        builder.Property(w => w.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
