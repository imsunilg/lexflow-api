using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ClientRelationshipConfiguration : IEntityTypeConfiguration<ClientRelationship>
{
    public void Configure(EntityTypeBuilder<ClientRelationship> builder)
    {
        builder.ToTable("client_relationships", "crm");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(r => r.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(r => r.RelatedClientId).HasColumnName("related_client_id");
        builder.Property(r => r.PersonName).HasColumnName("person_name");
        builder.Property(r => r.RelationType).HasColumnName("relation_type").IsRequired();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        builder.Property(r => r.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
