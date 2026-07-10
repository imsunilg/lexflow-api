using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ClientIdentityDocumentConfiguration : IEntityTypeConfiguration<ClientIdentityDocument>
{
    public void Configure(EntityTypeBuilder<ClientIdentityDocument> builder)
    {
        builder.ToTable("client_identity_documents", "crm");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(d => d.ClientId).HasColumnName("client_id").IsRequired();
        builder.Property(d => d.DocKind).HasColumnName("doc_kind").IsRequired();
        builder.Property(d => d.DocNumberEnc).HasColumnName("doc_number_enc").IsRequired();
        builder.Property(d => d.Last4).HasColumnName("last4").IsRequired();
        builder.Property(d => d.ExpiryDate).HasColumnName("expiry_date");
        builder.Property(d => d.DocumentId).HasColumnName("document_id");
        builder.Property(d => d.VerifyStatus).HasColumnName("verify_status").IsRequired();
        builder.Property(d => d.VerifiedBy).HasColumnName("verified_by");
        builder.Property(d => d.VerifiedAt).HasColumnName("verified_at");

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
