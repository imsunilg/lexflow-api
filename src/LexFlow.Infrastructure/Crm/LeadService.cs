using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Domain.Entities;
using LexFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LexFlow.Infrastructure.Crm;

/// <summary>Module 2 (Lead Management) — PRD §17 API list.</summary>
public sealed class LeadService(LexFlowDbContext db, IWorkflowEventPublisher? workflowEvents = null) : ILeadService
{
    private static readonly string[] PipelineOrder =
        ["New", "Contacted", "Consultation Scheduled", "Consultation Done", "Proposal Sent", "Negotiation", "Won(Converted)", "Lost"];

    public async Task<LeadDto> CreateAsync(Guid tenantId, Guid? actorId, CreateLeadInput input, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(input.PhoneE164))
        {
            var existing = await db.Leads.SingleOrDefaultAsync(
                l => l.TenantId == tenantId && l.PhoneE164 == input.PhoneE164 && l.Status == "Open", cancellationToken);
            if (existing is not null)
            {
                throw new ConflictException($"An open lead with this phone number already exists (id: {existing.Id}).", "LEAD_DUPLICATE_PHONE");
            }
        }

        var number = await GenerateNumberAsync(tenantId, "LD", cancellationToken);
        var lead = new Lead(
            tenantId,
            number,
            input.FirstName,
            input.LastName,
            input.Company,
            input.Email,
            input.PhoneE164,
            input.SourceId,
            input.OwnerId,
            input.BranchId,
            input.PracticeAreaId,
            input.IssueSummary,
            input.OpposingParty,
            input.BudgetBand,
            DateTimeOffset.UtcNow.AddHours(4));

        await db.Leads.AddAsync(lead, cancellationToken);

        if (workflowEvents is not null)
        {
            await workflowEvents.PublishAsync(tenantId, "lead.created", lead.Id, new
            {
                entityId = lead.Id,
                practiceAreaId = lead.PracticeAreaId,
                branchId = lead.BranchId,
                ownerId = lead.OwnerId,
            }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(lead);
    }

    public async Task<LeadDto?> GetByIdAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default)
    {
        var lead = await db.Leads.SingleOrDefaultAsync(l => l.TenantId == tenantId && l.Id == leadId, cancellationToken);
        return lead is null ? null : ToDto(lead);
    }

    public async Task<IReadOnlyList<LeadDto>> GetAllAsync(Guid tenantId, LeadFilter filter, CancellationToken cancellationToken = default)
    {
        var query = db.Leads.Where(l => l.TenantId == tenantId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Stage))
        {
            query = query.Where(l => l.Stage == filter.Stage);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(l => l.Status == filter.Status);
        }

        if (filter.SourceId.HasValue)
        {
            query = query.Where(l => l.SourceId == filter.SourceId);
        }

        if (filter.OwnerId.HasValue)
        {
            query = query.Where(l => l.OwnerId == filter.OwnerId);
        }

        if (filter.MinScore.HasValue)
        {
            query = query.Where(l => l.Score >= filter.MinScore);
        }

        if (filter.CreatedFrom.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= filter.CreatedFrom);
        }

        if (filter.CreatedTo.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= filter.CreatedTo);
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query;
            query = query.Where(l => l.FirstName.Contains(q) || (l.LastName != null && l.LastName.Contains(q)) || (l.Email != null && l.Email.Contains(q)));
        }

        var leads = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);
        return leads.Select(ToDto).ToList();
    }

    public async Task<LeadDto> UpdateAsync(Guid tenantId, Guid leadId, UpdateLeadInput input, CancellationToken cancellationToken = default)
    {
        var lead = await GetOrThrowAsync(tenantId, leadId, cancellationToken);
        lead.Update(
            input.FirstName,
            input.LastName,
            input.Company,
            input.Email,
            input.PhoneE164,
            input.SourceId,
            input.PracticeAreaId,
            input.IssueSummary,
            input.OpposingParty,
            input.BudgetBand);

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(lead);
    }

    public async Task DeleteAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken = default)
    {
        var lead = await GetOrThrowAsync(tenantId, leadId, cancellationToken);
        db.Leads.Remove(lead);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeadDto> ChangeStageAsync(Guid tenantId, Guid? actorId, Guid leadId, string toStage, string? note, bool allowSkip, CancellationToken cancellationToken = default)
    {
        var lead = await GetOrThrowAsync(tenantId, leadId, cancellationToken);
        var fromStage = lead.Stage;

        if (!allowSkip && !IsAdjacentOrConfigured(fromStage, toStage))
        {
            throw new ConflictException($"Stage transition '{fromStage}' -> '{toStage}' skips pipeline stages; requires lead.stage.skip.", "STAGE_SKIP_NOT_ALLOWED");
        }

        lead.ChangeStage(toStage);
        await db.LeadStageHistory.AddAsync(new LeadStageHistory(tenantId, leadId, fromStage, toStage, actorId), cancellationToken);

        if (!string.IsNullOrWhiteSpace(note))
        {
            await db.LeadActivities.AddAsync(new LeadActivity(tenantId, leadId, "note", null, null, "Stage change note", note, null, actorId), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(lead);
    }

    public async Task<LeadActivityDto> AddActivityAsync(Guid tenantId, Guid? actorId, Guid leadId, string activityType, string? direction, int? durationMin, string? subject, string? body, string? outcome, CancellationToken cancellationToken = default)
    {
        await GetOrThrowAsync(tenantId, leadId, cancellationToken);

        var activity = new LeadActivity(tenantId, leadId, activityType, direction, durationMin, subject, body, outcome, actorId);
        await db.LeadActivities.AddAsync(activity, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToActivityDto(activity);
    }

    public async Task<LeadDto> AssignAsync(Guid tenantId, Guid leadId, Guid? userId, Guid? ruleId, CancellationToken cancellationToken = default)
    {
        var lead = await GetOrThrowAsync(tenantId, leadId, cancellationToken);

        if (userId is null)
        {
            // Rule-based round-robin needs a modeled assignment-rule/team-membership graph
            // that doesn't exist in this build yet — fail loudly rather than fake a rule engine.
            throw new ConflictException("Rule-based auto-assignment is not modeled in this build; pass an explicit userId.", "ASSIGNMENT_RULE_NOT_SUPPORTED");
        }

        lead.Assign(userId.Value);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(lead);
    }

    public async Task<ConvertLeadResult> ConvertAsync(Guid tenantId, Guid? actorId, Guid leadId, bool createMatter, string? matterPayloadJson, string? invoicePayloadJson, bool force, CancellationToken cancellationToken = default)
    {
        if (createMatter || !string.IsNullOrWhiteSpace(invoicePayloadJson))
        {
            throw new ConflictException(
                "Matter/Invoice creation isn't available yet — the Legal (04_Legal) and Fin (06_Fin) modules haven't been built in this codebase (they ship in later build prompts). Convert with createMatter=false and no invoicePayload to create the Client alone.",
                "MODULE_NOT_AVAILABLE");
        }

        var lead = await GetOrThrowAsync(tenantId, leadId, cancellationToken);

        if (lead.Status != "Open")
        {
            throw new ConflictException($"Lead is already {lead.Status} and cannot be converted again.", "LEAD_NOT_OPEN");
        }

        if (!force && Array.IndexOf(PipelineOrder, lead.Stage) < Array.IndexOf(PipelineOrder, "Consultation Done"))
        {
            throw new ConflictException("Convert requires stage >= Consultation Done; requires lead.convert.force to override.", "CONVERT_STAGE_TOO_EARLY");
        }

        // BeginTransactionAsync is relational-only — the EF InMemory provider (unit tests)
        // has no real transaction support, so this no-ops there (SaveChanges is already
        // atomic per-call under InMemory; against Postgres this wraps both writes for AC-L3).
        var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(cancellationToken) : null;

        var isCorporate = !string.IsNullOrWhiteSpace(lead.Company);
        var number = await GenerateNumberAsync(tenantId, "CL", cancellationToken);
        var client = new Client(
            tenantId,
            number,
            isCorporate ? "Corporate" : "Individual",
            isCorporate ? null : lead.FirstName,
            isCorporate ? null : lead.LastName,
            isCorporate ? lead.Company : null,
            lead.Email,
            lead.PhoneE164,
            gstin: null,
            cin: null,
            ownerId: lead.OwnerId,
            branchId: lead.BranchId,
            sourceLeadId: lead.Id);

        await db.Clients.AddAsync(client, cancellationToken);

        var fromStage = lead.Stage;
        lead.ChangeStage("Won(Converted)");
        lead.MarkConverted(client.Id);
        await db.LeadStageHistory.AddAsync(new LeadStageHistory(tenantId, leadId, fromStage, "Won(Converted)", actorId), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            await transaction.DisposeAsync();
        }

        return new ConvertLeadResult(client.Id, null, null);
    }

    public async Task<LeadDto> MarkLostAsync(Guid tenantId, Guid? actorId, Guid leadId, Guid lostReasonId, string? note, CancellationToken cancellationToken = default)
    {
        var lead = await GetOrThrowAsync(tenantId, leadId, cancellationToken);

        var reasonExists = await db.LostReasons.AnyAsync(r => r.TenantId == tenantId && r.Id == lostReasonId, cancellationToken);
        if (!reasonExists)
        {
            throw new NotFoundException(nameof(LostReason), lostReasonId);
        }

        var fromStage = lead.Stage;
        lead.MarkLost(lostReasonId);
        await db.LeadStageHistory.AddAsync(new LeadStageHistory(tenantId, leadId, fromStage, "Lost", actorId), cancellationToken);

        if (!string.IsNullOrWhiteSpace(note))
        {
            await db.LeadActivities.AddAsync(new LeadActivity(tenantId, leadId, "note", null, null, "Lost reason note", note, null, actorId), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(lead);
    }

    public async Task<IReadOnlyList<DuplicateMatchDto>> CheckDuplicatesAsync(Guid tenantId, string? name, string? email, string? phone, CancellationToken cancellationToken = default)
    {
        var results = new List<DuplicateMatchDto>();

        if (!string.IsNullOrWhiteSpace(phone))
        {
            var phoneMatches = await db.Leads.Where(l => l.TenantId == tenantId && l.PhoneE164 == phone).ToListAsync(cancellationToken);
            results.AddRange(phoneMatches.Select(l => new DuplicateMatchDto(l.Id, DisplayName(l), l.Email, l.PhoneE164, 1.0, "Phone")));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailMatches = await db.Leads
                .Where(l => l.TenantId == tenantId && l.Email == email && !results.Select(r => r.LeadId).Contains(l.Id))
                .ToListAsync(cancellationToken);
            results.AddRange(emailMatches.Select(l => new DuplicateMatchDto(l.Id, DisplayName(l), l.Email, l.PhoneE164, 1.0, "Email")));
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            // pg_trgm fuzzy match against the ix_leads_name_trgm GIN index (Postgres only —
            // raw SQL isn't translatable against the InMemory provider used in unit tests).
            var fuzzy = await db.Leads.FromSqlInterpolated($@"
                SELECT * FROM crm.leads
                WHERE tenant_id = {tenantId} AND is_deleted = false
                  AND similarity(coalesce(first_name, '') || ' ' || coalesce(last_name, ''), {name}) > 0.3
                ORDER BY similarity(coalesce(first_name, '') || ' ' || coalesce(last_name, ''), {name}) DESC
                LIMIT 20")
                .Where(l => !results.Select(r => r.LeadId).Contains(l.Id))
                .ToListAsync(cancellationToken);

            results.AddRange(fuzzy.Select(l => new DuplicateMatchDto(l.Id, DisplayName(l), l.Email, l.PhoneE164, ComputeNameSimilarity(DisplayName(l), name), "Name")));
        }

        return results;
    }

    public async Task<byte[]> ExportAsync(Guid tenantId, LeadFilter filter, string format, CancellationToken cancellationToken = default)
    {
        var leads = await GetAllAsync(tenantId, filter, cancellationToken);
        return format.Equals("xlsx", StringComparison.OrdinalIgnoreCase)
            ? LeadCsvXlsx.ToXlsx(leads)
            : LeadCsvXlsx.ToCsv(leads);
    }

    public async Task<LeadDto> CaptureFromWebToLeadAsync(Guid tenantId, WebToLeadInput input, CancellationToken cancellationToken = default)
    {
        var number = await GenerateNumberAsync(tenantId, "LD", cancellationToken);
        var lead = new Lead(
            tenantId,
            number,
            input.FirstName,
            input.LastName,
            company: null,
            input.Email,
            input.PhoneE164,
            sourceId: null,
            ownerId: null,
            branchId: null,
            practiceAreaId: null,
            input.IssueSummary,
            opposingParty: null,
            budgetBand: null,
            DateTimeOffset.UtcNow.AddHours(4));

        await db.Leads.AddAsync(lead, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(lead);
    }

    private static bool IsAdjacentOrConfigured(string fromStage, string toStage)
    {
        var fromIndex = Array.IndexOf(PipelineOrder, fromStage);
        var toIndex = Array.IndexOf(PipelineOrder, toStage);

        // Unrecognized stages are tenant-configured custom stages (Module 2: "Pipeline
        // ... configurable") — always allowed since there's no fixed ordering to violate.
        if (fromIndex < 0 || toIndex < 0)
        {
            return true;
        }

        return Math.Abs(toIndex - fromIndex) <= 1;
    }

    private async Task<Lead> GetOrThrowAsync(Guid tenantId, Guid leadId, CancellationToken cancellationToken)
        => await db.Leads.SingleOrDefaultAsync(l => l.TenantId == tenantId && l.Id == leadId, cancellationToken)
           ?? throw new NotFoundException(nameof(Lead), leadId);

    private async Task<string> GenerateNumberAsync(Guid tenantId, string prefix, CancellationToken cancellationToken)
    {
        var count = prefix == "LD"
            ? await db.Leads.IgnoreQueryFilters().CountAsync(l => l.TenantId == tenantId, cancellationToken)
            : await db.Clients.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId, cancellationToken);

        return $"{prefix}-{DateTimeOffset.UtcNow.Year}-{count + 1:D6}";
    }

    private static string DisplayName(Lead lead) => string.Join(' ', new[] { lead.FirstName, lead.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static double ComputeNameSimilarity(string a, string name)
    {
        var bigramsA = Bigrams(a.ToLowerInvariant());
        var bigramsB = Bigrams(name.ToLowerInvariant());
        if (bigramsA.Count == 0 || bigramsB.Count == 0)
        {
            return 0;
        }

        var intersection = bigramsA.Intersect(bigramsB).Count();
        var union = bigramsA.Union(bigramsB).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static HashSet<string> Bigrams(string value)
    {
        var set = new HashSet<string>();
        for (var i = 0; i < value.Length - 1; i++)
        {
            set.Add(value.Substring(i, 2));
        }

        return set;
    }

    private static LeadActivityDto ToActivityDto(LeadActivity activity) => new(
        activity.Id, activity.LeadId, activity.ActivityType, activity.Direction, activity.DurationMin, activity.Subject, activity.Body, activity.Outcome, activity.OccurredAt, activity.LoggedBy);

    private static LeadDto ToDto(Lead lead) => new(
        lead.Id,
        lead.Number,
        lead.FirstName,
        lead.LastName,
        lead.Company,
        lead.Email,
        lead.PhoneE164,
        lead.SourceId,
        lead.Stage,
        lead.OwnerId,
        lead.BranchId,
        lead.PracticeAreaId,
        lead.Score,
        lead.IssueSummary,
        lead.OpposingParty,
        lead.BudgetBand,
        lead.Status,
        lead.LostReasonId,
        lead.ConvertedClientId,
        lead.SlaFirstContactDue,
        lead.FirstContactedAt,
        lead.CreatedAt);
}
