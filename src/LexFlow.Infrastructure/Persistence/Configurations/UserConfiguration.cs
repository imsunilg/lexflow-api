using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", "core");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasColumnType("citext").IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");
        builder.Property(u => u.Name).HasColumnName("name").IsRequired();
        builder.Property(u => u.Designation).HasColumnName("designation");
        builder.Property(u => u.BarEnrollmentNo).HasColumnName("bar_enrollment_no");
        builder.Property(u => u.Phone).HasColumnName("phone");
        builder.Property(u => u.PhotoBlobPath).HasColumnName("photo_blob_path");
        builder.Property(u => u.SignatureBlobPath).HasColumnName("signature_blob_path");
        builder.Property(u => u.CostRate).HasColumnName("cost_rate").HasColumnType("numeric(18,2)");
        builder.Property(u => u.BranchId).HasColumnName("branch_id");
        builder.Property(u => u.DepartmentId).HasColumnName("department_id");
        builder.Property(u => u.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired();
        builder.Property(u => u.Tz).HasColumnName("tz");
        builder.Property(u => u.Locale).HasColumnName("locale");
        builder.Property(u => u.TwoFaSecret).HasColumnName("two_fa_secret").HasColumnType("bytea");
        builder.Property(u => u.TwoFaEnabled).HasColumnName("two_fa_enabled").IsRequired();
        builder.Property(u => u.NotificationPrefs).HasColumnName("notification_prefs").HasColumnType("jsonb").IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by");
        builder.Property(u => u.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");
        builder.Property(u => u.DeletedBy).HasColumnName("deleted_by");

        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
