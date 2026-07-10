using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class CasePartyConfiguration : IEntityTypeConfiguration<CaseParty>
{
    public void Configure(EntityTypeBuilder<CaseParty> builder)
    {
        builder.ToTable("case_parties", "legal");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(p => p.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(p => p.PartyRole).HasColumnName("party_role").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").IsRequired();
        builder.Property(p => p.AdvocateName).HasColumnName("advocate_name");
        builder.Property(p => p.AdvocateUserId).HasColumnName("advocate_user_id");
        builder.Property(p => p.ContactJson).HasColumnName("contact").HasColumnType("jsonb").IsRequired();

        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        builder.Property(p => p.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
