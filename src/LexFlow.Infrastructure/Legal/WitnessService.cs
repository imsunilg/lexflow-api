using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Legal;

public sealed class WitnessService(LexFlowDbContext db) : IWitnessService
{
    public async Task<WitnessDto> AddAsync(Guid tenantId, Guid caseId, string name, string? side, string contactJson, DateOnly? scheduledOn, CancellationToken cancellationToken = default)
    {
        var exists = await db.CourtCases.AnyAsync(c => c.TenantId == tenantId && c.Id == caseId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(CourtCase), caseId);
        }

        var witness = new Witness(tenantId, caseId, name, side, contactJson, scheduledOn);
        await db.Witnesses.AddAsync(witness, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(witness);
    }

    public async Task<WitnessDto> UpdateAsync(Guid tenantId, Guid caseId, Guid witnessId, string name, string? side, string contactJson, DateOnly? scheduledOn, string? examStatus, CancellationToken cancellationToken = default)
    {
        var witness = await db.Witnesses.SingleOrDefaultAsync(w => w.TenantId == tenantId && w.CaseId == caseId && w.Id == witnessId, cancellationToken)
            ?? throw new NotFoundException(nameof(Witness), witnessId);

        witness.Update(name, side, contactJson, scheduledOn);
        if (!string.IsNullOrWhiteSpace(examStatus))
        {
            witness.SetExamStatus(examStatus);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(witness);
    }

    public async Task<IReadOnlyList<WitnessDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var witnesses = await db.Witnesses.Where(w => w.TenantId == tenantId && w.CaseId == caseId).ToListAsync(cancellationToken);
        return witnesses.Select(ToDto).ToList();
    }

    private static WitnessDto ToDto(Witness w) => new(w.Id, w.CaseId, w.Name, w.Side, w.ContactJson, w.ExamStatus, w.ScheduledOn);
}
