using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class MatterTeamMemberConfiguration : IEntityTypeConfiguration<MatterTeamMember>
{
    public void Configure(EntityTypeBuilder<MatterTeamMember> builder)
    {
        builder.ToTable("matter_team_members", "legal");
        builder.HasKey(m => new { m.MatterId, m.UserId });

        builder.Property(m => m.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(m => m.MatterId).HasColumnName("matter_id");
        builder.Property(m => m.UserId).HasColumnName("user_id");
        builder.Property(m => m.RoleInMatter).HasColumnName("role_in_matter");
        builder.Property(m => m.RateOverride).HasColumnName("rate_override").HasColumnType("numeric(18,4)");

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
