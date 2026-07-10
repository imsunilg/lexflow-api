using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("team_members", "core");
        builder.HasKey(tm => new { tm.TeamId, tm.UserId });

        builder.Property(tm => tm.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(tm => tm.TeamId).HasColumnName("team_id");
        builder.Property(tm => tm.UserId).HasColumnName("user_id");

        builder.Property(tm => tm.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(tm => tm.CreatedBy).HasColumnName("created_by");
        builder.Property(tm => tm.UpdatedAt).HasColumnName("updated_at");
        builder.Property(tm => tm.UpdatedBy).HasColumnName("updated_by");
        builder.Property(tm => tm.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(tm => tm.DeletedAt).HasColumnName("deleted_at");
        builder.Property(tm => tm.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(tm => !tm.IsDeleted);
    }
}
