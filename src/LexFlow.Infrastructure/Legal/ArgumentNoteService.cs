using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Legal;

public sealed class ArgumentNoteService(LexFlowDbContext db) : IArgumentNoteService
{
    public async Task<ArgumentNoteDto> AddAsync(Guid tenantId, Guid caseId, Guid? hearingId, string? stage, string body, IReadOnlyList<Guid>? citationJudgmentIds, CancellationToken cancellationToken = default)
    {
        var exists = await db.CourtCases.AnyAsync(c => c.TenantId == tenantId && c.Id == caseId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(nameof(CourtCase), caseId);
        }

        var note = new ArgumentNote(tenantId, caseId, hearingId, stage, body, citationJudgmentIds?.ToArray());
        await db.ArgumentNotes.AddAsync(note, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(note);
    }

    public async Task<IReadOnlyList<ArgumentNoteDto>> GetByCaseAsync(Guid tenantId, Guid caseId, CancellationToken cancellationToken = default)
    {
        var notes = await db.ArgumentNotes.Where(a => a.TenantId == tenantId && a.CaseId == caseId).ToListAsync(cancellationToken);
        return notes.Select(ToDto).ToList();
    }

    private static ArgumentNoteDto ToDto(ArgumentNote a) => new(a.Id, a.CaseId, a.HearingId, a.Stage, a.Body, a.CitationJudgmentIds);
}
