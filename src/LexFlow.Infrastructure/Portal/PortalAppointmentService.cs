using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Portal;

/// <summary>Module 17 User Flow #6: appointment requests. Validation: "appointment requests >= 24h ahead."</summary>
public sealed class PortalAppointmentService(LexFlowDbContext db) : IPortalAppointmentService
{
    private static readonly TimeSpan MinimumLeadTime = TimeSpan.FromHours(24);

    public async Task<IReadOnlyList<PortalAppointmentDto>> GetAppointmentsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default)
    {
        var portalUserIds = await db.ClientPortalUsers
            .Where(u => u.TenantId == tenantId && u.ClientId == clientId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var appointments = await db.PortalAppointmentRequests
            .Where(a => a.TenantId == tenantId && portalUserIds.Contains(a.ClientPortalUserId))
            .OrderByDescending(a => a.RequestedStart)
            .ToListAsync(cancellationToken);

        return appointments.Select(ToDto).ToList();
    }

    public async Task<PortalAppointmentDto> RequestAppointmentAsync(Guid tenantId, Guid clientPortalUserId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid matterId, Guid lawyerId, DateTimeOffset requestedStart, DateTimeOffset requestedEnd, string? notes, CancellationToken cancellationToken = default)
    {
        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken);
        if (matter is null || matter.ClientId != clientId || (visibleMatterIds is not null && !visibleMatterIds.Contains(matter.Id)))
        {
            throw new NotFoundException(nameof(Matter), matterId);
        }

        if (requestedStart < DateTimeOffset.UtcNow.Add(MinimumLeadTime))
        {
            throw new DomainRuleException("APPOINTMENT_TOO_SOON", "Appointment requests must be at least 24 hours ahead.");
        }

        var appointment = new PortalAppointmentRequest(tenantId, clientPortalUserId, matterId, lawyerId, requestedStart, requestedEnd, notes);
        await db.PortalAppointmentRequests.AddAsync(appointment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(appointment);
    }

    private static PortalAppointmentDto ToDto(PortalAppointmentRequest a) => new(a.Id, a.MatterId, a.LawyerId, a.RequestedStart, a.RequestedEnd, a.Status, a.ConfirmedStart, a.ConfirmedEnd);
}
