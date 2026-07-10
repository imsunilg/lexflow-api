namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 5 order register — PRD §17 (POST/GET /cases/{id}/orders).</summary>
public interface ICourtOrderService
{
    Task<CourtOrderDto> AddAsync(Guid tenantId, Guid caseId, Guid? hearingId, DateOnly orderDate, string? gist, DateOnly? complianceDue, Guid? documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CourtOrderDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default);
}

public sealed record CourtOrderDto(Guid Id, Guid CaseId, Guid? HearingId, DateOnly OrderDate, string? Gist, DateOnly? ComplianceDue, Guid? DocumentId);
