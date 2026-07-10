namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 17 User Flow #6: appointment requests. Validation: requests must be &gt;= 24h ahead.</summary>
public interface IPortalAppointmentService
{
    Task<IReadOnlyList<PortalAppointmentDto>> GetAppointmentsAsync(Guid tenantId, Guid clientId, CancellationToken cancellationToken = default);

    /// <summary>Throws DomainRuleException("APPOINTMENT_TOO_SOON", ...) if requestedStart is less than 24h from now.</summary>
    Task<PortalAppointmentDto> RequestAppointmentAsync(Guid tenantId, Guid clientPortalUserId, Guid clientId, IReadOnlyCollection<Guid>? visibleMatterIds, Guid matterId, Guid lawyerId, DateTimeOffset requestedStart, DateTimeOffset requestedEnd, string? notes, CancellationToken cancellationToken = default);
}

public sealed record PortalAppointmentDto(Guid Id, Guid MatterId, Guid LawyerId, DateTimeOffset RequestedStart, DateTimeOffset RequestedEnd, string Status, DateTimeOffset? ConfirmedStart, DateTimeOffset? ConfirmedEnd);
