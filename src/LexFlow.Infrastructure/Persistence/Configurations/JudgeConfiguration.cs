using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class JudgeConfiguration : IEntityTypeConfiguration<Judge>
{
    public void Configure(EntityTypeBuilder<Judge> builder)
    {
        builder.ToTable("judges", "legal");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id).HasColumnName("id");
        builder.Property(j => j.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(j => j.Name).HasColumnName("name").IsRequired();
        builder.Property(j => j.CourtId).HasColumnName("court_id").IsRequired();
        builder.Property(j => j.Active).HasColumnName("active").IsRequired();

        builder.Property(j => j.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(j => j.CreatedBy).HasColumnName("created_by");
        builder.Property(j => j.UpdatedAt).HasColumnName("updated_at");
        builder.Property(j => j.UpdatedBy).HasColumnName("updated_by");
        builder.Property(j => j.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(j => j.DeletedAt).HasColumnName("deleted_at");
        builder.Property(j => j.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(j => !j.IsDeleted);
    }
}
