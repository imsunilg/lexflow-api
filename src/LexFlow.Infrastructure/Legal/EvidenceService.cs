using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Legal;

/// <summary>Module 5 evidence register. AC-CC5: custody log is append-only — the DB trigger on legal.evidence_custody_log is the hard backstop; this service just never issues UPDATE/DELETE against it.</summary>
public sealed class EvidenceService(LexFlowDbContext db) : IEvidenceService
{
    public async Task<EvidenceItemDto> AddAsync(Guid tenantId, Guid caseId, string? exhibitNo, string kind, string? description, Guid? documentId, CancellationToken cancellationToken = default)
    {
        var exists = await db.CourtCases.AnyAsync(c => c.TenantId == tenantId && c.Id == caseId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(CourtCase), caseId);
        }

        var item = new EvidenceItem(tenantId, caseId, exhibitNo, kind, description, documentId);
        await db.EvidenceItems.AddAsync(item, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<IReadOnlyList<EvidenceItemDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var items = await db.EvidenceItems.Where(e => e.TenantId == tenantId && e.CaseId == caseId).ToListAsync(cancellationToken);
        return items.Select(ToDto).ToList();
    }

    public async Task<EvidenceCustodyLogDto> AddCustodyEventAsync(Guid tenantId, Guid evidenceId, string action, string? holder, string? note, CancellationToken cancellationToken = default)
    {
        var evidence = await db.EvidenceItems.SingleOrDefaultAsync(e => e.TenantId == tenantId && e.Id == evidenceId, cancellationToken)
            ?? throw new NotFoundException(nameof(EvidenceItem), evidenceId);

        var entry = new EvidenceCustodyLog(tenantId, evidenceId, action, holder, note);
        await db.EvidenceCustodyLog.AddAsync(entry, cancellationToken);

        evidence.SetCustodyStatus(action);

        await db.SaveChangesAsync(cancellationToken);
        return ToCustodyDto(entry);
    }

    public async Task<IReadOnlyList<EvidenceCustodyLogDto>> GetCustodyChainAsync(Guid tenantId, Guid evidenceId, CancellationToken cancellationToken = default)
    {
        var entries = await db.EvidenceCustodyLog.Where(l => l.TenantId == tenantId && l.EvidenceId == evidenceId).OrderBy(l => l.At).ToListAsync(cancellationToken);
        return entries.Select(ToCustodyDto).ToList();
    }

    private static EvidenceItemDto ToDto(EvidenceItem e) => new(e.Id, e.CaseId, e.ExhibitNo, e.Kind, e.Description, e.Marked, e.Objected, e.CustodyStatus, e.DocumentId);

    private static EvidenceCustodyLogDto ToCustodyDto(EvidenceCustodyLog l) => new(l.Id, l.EvidenceId, l.Action, l.Holder, l.At, l.Note);
}
