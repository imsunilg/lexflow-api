using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients", "crm");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(c => c.Number).HasColumnName("number").IsRequired();
        builder.Property(c => c.Type).HasColumnName("type").IsRequired();
        builder.Property(c => c.FirstName).HasColumnName("first_name");
        builder.Property(c => c.LastName).HasColumnName("last_name");
        builder.Property(c => c.LegalName).HasColumnName("legal_name");
        // DB-generated STORED column (CASE on type/first_name/last_name/legal_name) — EF never writes it.
        var displayName = builder.Property(c => c.DisplayName).HasColumnName("display_name").Metadata;
        displayName.SetBeforeSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
        displayName.SetAfterSaveBehavior(Microsoft.EntityFrameworkCore.Metadata.PropertySaveBehavior.Ignore);
        builder.Property(c => c.Email).HasColumnName("email");
        builder.Property(c => c.PhoneE164).HasColumnName("phone_e164");
        builder.Property(c => c.PanEnc).HasColumnName("pan_enc");
        builder.Property(c => c.Gstin).HasColumnName("gstin");
        builder.Property(c => c.Cin).HasColumnName("cin");
        builder.Property(c => c.Status).HasColumnName("status").IsRequired();
        builder.Property(c => c.CreditLimit).HasColumnName("credit_limit").HasColumnType("numeric(18,2)");
        builder.Property(c => c.OwnerId).HasColumnName("owner_id");
        builder.Property(c => c.BranchId).HasColumnName("branch_id");
        builder.Property(c => c.PortalEnabled).HasColumnName("portal_enabled").IsRequired();
        builder.Property(c => c.SourceLeadId).HasColumnName("source_lead_id");
        builder.Property(c => c.MergedIntoClientId).HasColumnName("merged_into_client_id");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");
        builder.Property(c => c.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
