using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ClientAddressConfiguration : IEntityTypeConfiguration<ClientAddress>
{
    public void Configure(EntityTypeBuilder<ClientAddress> builder)
    {
        builder.ToTable("client_addresses", "crm");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(a => a.Kind).HasColumnName("kind").IsRequired();
        builder.Property(a => a.Line1).HasColumnName("line1").IsRequired();
        builder.Property(a => a.Line2).HasColumnName("line2");
        builder.Property(a => a.City).HasColumnName("city");
        builder.Property(a => a.StateCode).HasColumnName("state_code");
        builder.Property(a => a.Postal).HasColumnName("postal");
        builder.Property(a => a.Country).HasColumnName("country");
        builder.Property(a => a.IsPrimaryOfKind).HasColumnName("is_primary_of_kind").IsRequired();

        builder.Property(a => a.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");
        builder.Property(a => a.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
