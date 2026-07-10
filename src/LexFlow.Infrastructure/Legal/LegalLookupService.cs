using LexFlow.Application.Common.Interfaces;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Legal;

public sealed class LegalLookupService(LexFlowDbContext db) : ILegalLookupService
{
    public async Task<IReadOnlyList<CourtDto>> GetCourtsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var courts = await db.Courts.Where(c => c.TenantId == tenantId).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        return courts.Select(c => new CourtDto(c.Id, c.Name, c.Level, c.City, c.State, c.Bench, c.Tz)).ToList();
    }

    public async Task<IReadOnlyList<JudgeDto>> GetJudgesAsync(Guid tenantId, Guid? courtId, CancellationToken cancellationToken = default)
    {
        var query = db.Judges.Where(j => j.TenantId == tenantId);
        if (courtId.HasValue)
        {
            query = query.Where(j => j.CourtId == courtId);
        }

        var judges = await query.OrderBy(j => j.Name).ToListAsync(cancellationToken);
        return judges.Select(j => new JudgeDto(j.Id, j.Name, j.CourtId, j.Active)).ToList();
    }

    public async Task<IReadOnlyList<PracticeAreaDto>> GetPracticeAreasAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var areas = await db.PracticeAreas.Where(p => p.TenantId == tenantId).OrderBy(p => p.Name).ToListAsync(cancellationToken);
        return areas.Select(p => new PracticeAreaDto(p.Id, p.Name, p.ParentId, p.IsActive)).ToList();
    }
}
