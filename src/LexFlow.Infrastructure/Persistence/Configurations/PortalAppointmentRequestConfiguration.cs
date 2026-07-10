using LexFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LexFlow.Infrastructure.Persistence.Configurations;

public sealed class PortalAppointmentRequestConfiguration : IEntityTypeConfiguration<PortalAppointmentRequest>
{
    public void Configure(EntityTypeBuilder<PortalAppointmentRequest> builder)
    {
        builder.ToTable("portal_appointment_requests", "portal");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(a => a.ClientPortalUserId).HasColumnName("client_portal_user_id").IsRequired();
        builder.Property(a => a.MatterId).HasColumnName("matter_id").IsRequired();
        builder.Property(a => a.LawyerId).HasColumnName("lawyer_id").IsRequired();
        builder.Property(a => a.RequestedStart).HasColumnName("requested_start").IsRequired();
        builder.Property(a => a.RequestedEnd).HasColumnName("requested_end").IsRequired();
        builder.Property(a => a.Notes).HasColumnName("notes");
        builder.Property(a => a.Status).HasColumnName("status").IsRequired();
        builder.Property(a => a.ConfirmedStart).HasColumnName("confirmed_start");
        builder.Property(a => a.ConfirmedEnd).HasColumnName("confirmed_end");
        builder.Property(a => a.CalendarEventId).HasColumnName("calendar_event_id");

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
