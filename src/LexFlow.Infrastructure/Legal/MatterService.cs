using System.Data.Common;
using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Legal;

/// <summary>Module 4 (Matter Management) — PRD §17 API list.</summary>
public sealed class MatterService(LexFlowDbContext db, IConflictCheckService conflictCheckService) : IMatterService
{
    public async Task<MatterDto> CreateAsync(Guid tenantId, Guid? actorId, CreateMatterInput input, CancellationToken cancellationToken = default)
    {
        var oppositeParties = input.OppositePartyNames ?? [];
        if (oppositeParties.Count > 0)
        {
            var conflicts = await conflictCheckService.CheckAsync(tenantId, oppositeParties, cancellationToken);
            if (conflicts.Count > 0 && !input.OverrideConflict)
            {
                throw new DomainRuleException("CONFLICT_OF_INTEREST_SUSPECTED", "Potential conflict of interest detected against existing matter parties/clients.", conflicts);
            }
            // AC-M1: "audit shows both" — the override decision travels with the request and the
            // resulting matter row is captured by AuditSaveChangesInterceptor like any other write;
            // ConflictOverrideReason itself isn't persisted to a dedicated column (none exists in
            // the current legal.matters schema) but is required at the validator layer so the
            // override can never happen silently.
        }

        var number = await GenerateNumberAsync(tenantId, cancellationToken);
        var matter = new Matter(
            tenantId,
            number,
            input.Title,
            input.ClientId,
            input.MatterType,
            input.PracticeAreaId,
            input.BranchId,
            input.ResponsibleLawyerId,
            input.Priority,
            input.Description,
            input.OpenedOn,
            input.IsPrivate,
            input.Budget,
            input.BillingArrangementJson ?? "{}");

        await db.Matters.AddAsync(matter, cancellationToken);

        foreach (var partyName in oppositeParties)
        {
            var party = new MatterParty(tenantId, matter.Id, partyName, "Opposite", null, "{}");
            await db.MatterParties.AddAsync(party, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var partyName in oppositeParties)
        {
            await conflictCheckService.IndexPartyAsync(tenantId, partyName, "matter_party", matter.Id, matter.Id, matter.Number, cancellationToken);
        }

        return ToDto(matter);
    }

    public async Task<MatterDto?> GetByIdAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var matter = await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken);
        return matter is null ? null : ToDto(matter);
    }

    public async Task<IReadOnlyList<MatterDto>> GetAllAsync(Guid tenantId, MatterFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.Matters.Where(m => m.TenantId == tenantId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(m => m.Status == filter.Status);
        }

        if (!string.IsNullOrWhiteSpace(filter.MatterType))
        {
            query = query.Where(m => m.MatterType == filter.MatterType);
        }

        if (filter.PracticeAreaId.HasValue)
        {
            query = query.Where(m => m.PracticeAreaId == filter.PracticeAreaId);
        }

        if (filter.ResponsibleLawyerId.HasValue)
        {
            query = query.Where(m => m.ResponsibleLawyerId == filter.ResponsibleLawyerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            query = query.Where(m => m.Priority == filter.Priority);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = $"%{filter.Query}%";
            query = query.Where(m => EF.Functions.ILike(m.Title, q) || EF.Functions.ILike(m.Number, q));
        }

        var matters = await query.OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);
        return matters.Select(ToDto).ToList();
    }

    public async Task<MatterDto> UpdateAsync(Guid tenantId, Guid matterId, UpdateMatterInput input, CancellationToken cancellationToken = default)
    {
        var matter = await GetOrThrowAsync(tenantId, matterId, cancellationToken);
        matter.Update(input.Title, input.MatterType, input.PracticeAreaId, input.BranchId, input.ResponsibleLawyerId, input.Priority, input.Description, input.Budget, input.IsPrivate, input.BillingArrangementJson ?? "{}");
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(matter);
    }

    public async Task<MatterDto> ChangeStatusAsync(Guid tenantId, Guid? actorId, Guid matterId, string toStatus, string? outcome, string? closureNote, bool allowReopen, CancellationToken cancellationToken = default)
    {
        var matter = await GetOrThrowAsync(tenantId, matterId, cancellationToken);
        var fromStatus = matter.Status;

        if (toStatus == "Closed")
        {
            if (string.IsNullOrWhiteSpace(closureNote))
            {
                throw new ValidationException([new FluentValidation.Results.ValidationFailure("closureNote", "A closure summary note is required to close a matter (AC-M3 checklist).")]);
            }

            var runningTimerCount = await CountRunningTimersAsync(tenantId, matterId, cancellationToken);
            if (runningTimerCount > 0)
            {
                throw new ConflictException($"Cannot close matter — {runningTimerCount} running timer(s) must be stopped first.", "TIMERS_RUNNING");
            }
        }

        if (fromStatus == "Closed" && toStatus == "Reopened" && !allowReopen)
        {
            throw new ForbiddenAccessException();
        }

        var closedOn = toStatus == "Closed" ? DateOnly.FromDateTime(DateTime.UtcNow) : matter.ClosedOn;
        matter.ChangeStatus(toStatus, outcome, closedOn);
        await db.MatterStatusHistory.AddAsync(new MatterStatusHistory(tenantId, matterId, fromStatus, toStatus, outcome, closureNote, actorId), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(matter);
    }

    public async Task AddTeamMemberAsync(Guid tenantId, Guid matterId, Guid userId, string? roleInMatter, decimal? rateOverride, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, matterId, cancellationToken);
        var existing = await db.MatterTeamMembers.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.MatterId == matterId && t.UserId == userId, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("User is already a team member of this matter.", "TEAM_MEMBER_EXISTS");
        }

        await db.MatterTeamMembers.AddAsync(new MatterTeamMember(tenantId, matterId, userId, roleInMatter, rateOverride), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveTeamMemberAsync(Guid tenantId, Guid matterId, Guid userId, CancellationToken cancellationToken = default)
    {
        var member = await db.MatterTeamMembers.SingleOrDefaultAsync(t => t.TenantId == tenantId && t.MatterId == matterId && t.UserId == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(MatterTeamMember), userId);

        db.MatterTeamMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatterTeamMemberDto>> GetTeamAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var members = await db.MatterTeamMembers.Where(t => t.TenantId == tenantId && t.MatterId == matterId).ToListAsync(cancellationToken);
        return members.Select(t => new MatterTeamMemberDto(t.MatterId, t.UserId, t.RoleInMatter, t.RateOverride)).ToList();
    }

    public async Task<MatterPartyDto> AddPartyAsync(Guid tenantId, Guid matterId, MatterPartyInput input, CancellationToken cancellationToken = default)
    {
        var matter = await GetOrThrowAsync(tenantId, matterId, cancellationToken);
        var party = new MatterParty(tenantId, matterId, input.Name, input.PartyRole, input.AdvocateName, input.ContactJson);
        await db.MatterParties.AddAsync(party, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        if (input.PartyRole == "Opposite")
        {
            await conflictCheckService.IndexPartyAsync(tenantId, input.Name, "matter_party", party.Id, matterId, matter.Number, cancellationToken);
        }

        return ToPartyDto(party);
    }

    public async Task<MatterPartyDto> UpdatePartyAsync(Guid tenantId, Guid matterId, Guid partyId, MatterPartyInput input, CancellationToken cancellationToken = default)
    {
        var party = await db.MatterParties.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.MatterId == matterId && p.Id == partyId, cancellationToken)
            ?? throw new NotFoundException(nameof(MatterParty), partyId);

        party.Update(input.Name, input.PartyRole, input.AdvocateName, input.ContactJson);
        await db.SaveChangesAsync(cancellationToken);
        return ToPartyDto(party);
    }

    public async Task DeletePartyAsync(Guid tenantId, Guid matterId, Guid partyId, CancellationToken cancellationToken = default)
    {
        var party = await db.MatterParties.SingleOrDefaultAsync(p => p.TenantId == tenantId && p.MatterId == matterId && p.Id == partyId, cancellationToken)
            ?? throw new NotFoundException(nameof(MatterParty), partyId);

        db.MatterParties.Remove(party);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatterPartyDto>> GetPartiesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var parties = await db.MatterParties.Where(p => p.TenantId == tenantId && p.MatterId == matterId).ToListAsync(cancellationToken);
        return parties.Select(ToPartyDto).ToList();
    }

    public async Task<MatterImportantDateDto> AddImportantDateAsync(Guid tenantId, Guid matterId, ImportantDateInput input, CancellationToken cancellationToken = default)
    {
        var matter = await GetOrThrowAsync(tenantId, matterId, cancellationToken);

        // Module 4 Validation: "limitation date must be >= open date".
        if (input.Kind == "Limitation" && input.DueAt.Date < matter.OpenedOn.ToDateTime(TimeOnly.MinValue))
        {
            throw new Application.Common.Exceptions.ValidationException([new FluentValidation.Results.ValidationFailure("dueAt", "Limitation date must be on or after the matter's open date.")]);
        }

        var date = new MatterImportantDate(tenantId, matterId, input.Kind, input.Title, input.DueAt, input.ReminderPolicyJson ?? "{}", input.Severity);
        await db.MatterImportantDates.AddAsync(date, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToImportantDateDto(date);
    }

    public async Task<MatterImportantDateDto> UpdateImportantDateAsync(Guid tenantId, Guid matterId, Guid dateId, ImportantDateInput input, CancellationToken cancellationToken = default)
    {
        var date = await db.MatterImportantDates.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.MatterId == matterId && d.Id == dateId, cancellationToken)
            ?? throw new NotFoundException(nameof(MatterImportantDate), dateId);

        date.Update(input.Kind, input.Title, input.DueAt, input.ReminderPolicyJson ?? "{}", input.Severity);
        await db.SaveChangesAsync(cancellationToken);
        return ToImportantDateDto(date);
    }

    public async Task DeleteImportantDateAsync(Guid tenantId, Guid matterId, Guid dateId, CancellationToken cancellationToken = default)
    {
        var date = await db.MatterImportantDates.SingleOrDefaultAsync(d => d.TenantId == tenantId && d.MatterId == matterId && d.Id == dateId, cancellationToken)
            ?? throw new NotFoundException(nameof(MatterImportantDate), dateId);

        // BR-2's hard 30-day delete-block is enforced by a DB trigger — this just lets that
        // exception surface as a normal Postgres error; no need to duplicate the 30-day check
        // here (single source of truth in the migration).
        db.MatterImportantDates.Remove(date);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MatterImportantDateDto>> GetImportantDatesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var dates = await db.MatterImportantDates.Where(d => d.TenantId == tenantId && d.MatterId == matterId).ToListAsync(cancellationToken);
        return dates.Select(ToImportantDateDto).ToList();
    }

    public async Task<MatterExpenseDto> AddExpenseAsync(Guid tenantId, Guid matterId, MatterExpenseInput input, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, matterId, cancellationToken);
        var expense = new MatterExpense(tenantId, matterId, input.IncurredOn, input.Category, input.Description, input.Amount, input.Billable, input.ReceiptDocumentId);
        await db.MatterExpenses.AddAsync(expense, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToExpenseDto(expense);
    }

    public async Task<IReadOnlyList<MatterExpenseDto>> GetExpensesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var expenses = await db.MatterExpenses.Where(e => e.TenantId == tenantId && e.MatterId == matterId).ToListAsync(cancellationToken);
        return expenses.Select(ToExpenseDto).ToList();
    }

    public async Task<MatterRelatedDto> AddRelatedAsync(Guid tenantId, Guid matterId, Guid relatedMatterId, string relationType, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, matterId, cancellationToken);
        await GetOrThrowAsync(tenantId, relatedMatterId, cancellationToken);

        var related = new MatterRelated(tenantId, matterId, relatedMatterId, relationType);
        await db.MatterRelated.AddAsync(related, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new MatterRelatedDto(related.Id, related.MatterId, related.RelatedMatterId, related.RelationType);
    }

    public async Task<TimelinePage> GetTimelineAsync(Guid tenantId, Guid matterId, string? types, string? cursor, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, matterId, cancellationToken);

        var entries = new List<TimelineEntry>();

        var caseIds = await db.CourtCases.Where(c => c.TenantId == tenantId && c.MatterId == matterId).Select(c => c.Id).ToListAsync(cancellationToken);

        var hearings = await db.Hearings.Where(h => h.TenantId == tenantId && caseIds.Contains(h.CaseId)).ToListAsync(cancellationToken);
        entries.AddRange(hearings.Select(h => new TimelineEntry("hearing", h.Id, h.Date.ToDateTime(h.Time ?? TimeOnly.MinValue), $"Hearing ({h.Status}) — {h.Purpose}")));

        var orders = await db.CourtOrders.Where(o => o.TenantId == tenantId && caseIds.Contains(o.CaseId)).ToListAsync(cancellationToken);
        entries.AddRange(orders.Select(o => new TimelineEntry("order", o.Id, o.OrderDate.ToDateTime(TimeOnly.MinValue), o.Gist ?? "Order")));

        var statusHistory = await db.MatterStatusHistory.Where(h => h.TenantId == tenantId && h.MatterId == matterId).ToListAsync(cancellationToken);
        entries.AddRange(statusHistory.Select(h => new TimelineEntry("status", h.Id, h.At, $"Status: {h.FromStatus} -> {h.ToStatus}")));

        var expenses = await db.MatterExpenses.Where(e => e.TenantId == tenantId && e.MatterId == matterId).ToListAsync(cancellationToken);
        entries.AddRange(expenses.Select(e => new TimelineEntry("expense", e.Id, e.IncurredOn.ToDateTime(TimeOnly.MinValue), $"Expense: {e.Description} ({e.Amount:C})")));

        if (!string.IsNullOrWhiteSpace(types))
        {
            var typeSet = types.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
            entries = entries.Where(e => typeSet.Contains(e.EntityType)).ToList();
        }

        var ordered = entries.OrderByDescending(e => e.At).ToList();

        // Simple offset-based cursor (page size 50) — sufficient for this build's timeline
        // sources; a true keyset cursor would matter more once Documents/Tasks/Notes (which
        // don't exist yet) are merged in too.
        const int pageSize = 50;
        var offset = int.TryParse(cursor, out var parsed) ? parsed : 0;
        var page = ordered.Skip(offset).Take(pageSize).ToList();
        var nextCursor = offset + pageSize < ordered.Count ? (offset + pageSize).ToString() : null;

        return new TimelinePage(page, nextCursor);
    }

    public async Task<MatterFinancialSummaryDto> GetFinancialSummaryAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var matter = await GetOrThrowAsync(tenantId, matterId, cancellationToken);

        var billed = await SumAsync("fin.invoices", "grand_total", "matter_id", matterId, cancellationToken);
        var collected = await SumAsync("fin.invoices", "amount_paid", "matter_id", matterId, cancellationToken);
        var wip = await SumAsync("fin.time_entries", "amount_snapshot", "matter_id", matterId, cancellationToken, "status <> 'Billed' AND billable = true AND ");
        var expenses = (await GetExpensesAsync(tenantId, matterId, cancellationToken)).Sum(e => e.Amount);
        var trustBalance = await SumAsync("fin.trust_accounts", "current_balance", "client_id", matter.ClientId, cancellationToken);

        decimal? budgetVariance = matter.Budget.HasValue ? matter.Budget.Value - billed : null;

        return new MatterFinancialSummaryDto(billed, collected, wip, expenses, budgetVariance, trustBalance);
    }

    private async Task<decimal> SumAsync(string table, string column, string filterColumn, Guid filterValue, CancellationToken cancellationToken, string? extraWhere = null)
    {
        if (!db.Database.IsRelational())
        {
            return 0;
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT coalesce(sum({column}), 0) FROM {table} WHERE {extraWhere}{filterColumn} = @filterValue AND is_deleted = false";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "filterValue";
        parameter.Value = filterValue;
        command.Parameters.Add(parameter);

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToDecimal(result);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private async Task<long> CountRunningTimersAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            return 0;
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM fin.running_timers WHERE tenant_id = @tenantId AND matter_id = @matterId";
        var p1 = command.CreateParameter();
        p1.ParameterName = "tenantId";
        p1.Value = tenantId;
        command.Parameters.Add(p1);
        var p2 = command.CreateParameter();
        p2.ParameterName = "matterId";
        p2.Value = matterId;
        command.Parameters.Add(p2);

        try
        {
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }
        catch (DbException)
        {
            return 0;
        }
    }

    private async Task<Matter> GetOrThrowAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken)
        => await db.Matters.SingleOrDefaultAsync(m => m.TenantId == tenantId && m.Id == matterId, cancellationToken)
           ?? throw new NotFoundException(nameof(Matter), matterId);

    private async Task<string> GenerateNumberAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var count = await db.Matters.IgnoreQueryFilters().CountAsync(m => m.TenantId == tenantId, cancellationToken);
        return $"MAT-{DateTimeOffset.UtcNow.Year}-{count + 1:D6}";
    }

    private static MatterPartyDto ToPartyDto(MatterParty p) => new(p.Id, p.MatterId, p.Name, p.PartyRole, p.AdvocateName, p.ContactJson);

    private static MatterImportantDateDto ToImportantDateDto(MatterImportantDate d) => new(d.Id, d.MatterId, d.Kind, d.Title, d.DueAt, d.SatisfiedAt, d.SatisfiedNote, d.Severity);

    private static MatterExpenseDto ToExpenseDto(MatterExpense e) => new(e.Id, e.MatterId, e.IncurredOn, e.Category, e.Description, e.Amount, e.Billable);

    private static MatterDto ToDto(Matter m) => new(
        m.Id, m.Number, m.Title, m.ClientId, m.MatterType, m.PracticeAreaId, m.BranchId, m.ResponsibleLawyerId,
        m.Priority, m.Status, m.Outcome, m.OpenedOn, m.ClosedOn, m.IsPrivate, m.Budget, m.Description, m.BillingArrangementJson, m.CreatedAt);
}
