using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class MatterImportantDateConfiguration : IEntityTypeConfiguration<MatterImportantDate>
{
    public void Configure(EntityTypeBuilder<MatterImportantDate> builder)
    {
        builder.ToTable("matter_important_dates", "legal");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(d => d.Kind).HasColumnName("kind").IsRequired();
        builder.Property(d => d.Title).HasColumnName("title").IsRequired();
        builder.Property(d => d.DueAt).HasColumnName("due_at").IsRequired();
        builder.Property(d => d.SatisfiedAt).HasColumnName("satisfied_at");
        builder.Property(d => d.SatisfiedNote).HasColumnName("satisfied_note");
        builder.Property(d => d.ReminderPolicyJson).HasColumnName("reminder_policy").HasColumnType("jsonb").IsRequired();
        builder.Property(d => d.Severity).HasColumnName("severity");

        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(d => d.DeletedAt).HasColumnName("deleted_at");
        builder.Property(d => d.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
