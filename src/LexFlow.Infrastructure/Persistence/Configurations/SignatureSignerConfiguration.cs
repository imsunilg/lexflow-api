using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class SignatureSignerConfiguration : IEntityTypeConfiguration<SignatureSigner>
{
    public void Configure(EntityTypeBuilder<SignatureSigner> builder)
    {
        builder.ToTable("signature_signers", "dms");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(s => s.EnvelopeId).HasColumnName("envelope_id").IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").IsRequired();
        builder.Property(s => s.Email).HasColumnName("email").IsRequired();
        builder.Property(s => s.OrderNo).HasColumnName("order_no").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").IsRequired();
        builder.Property(s => s.SignedAt).HasColumnName("signed_at");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");
        builder.Property(s => s.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
