namespace LexFlow.Application.Common.Interfaces;

/// <summary>Module 5 witness list — PRD §17 (POST/PUT /cases/{id}/witnesses).</summary>
public interface IWitnessService
{
    Task<WitnessDto> AddAsync(Guid tenantId, Guid caseId, string name, string? side, string contactJson, DateOnly? scheduledOn, CancellationToken cancellationToken = default);

    Task<WitnessDto> UpdateAsync(Guid tenantId, Guid caseId, Guid witnessId, string name, string? side, string contactJson, DateOnly? scheduledOn, string? examStatus, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WitnessDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default);
}

public sealed record WitnessDto(Guid Id, Guid CaseId, string Name, string? Side, string ContactJson, string ExamStatus, DateOnly? ScheduledOn);
