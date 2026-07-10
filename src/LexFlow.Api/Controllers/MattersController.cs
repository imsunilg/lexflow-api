using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Cases;
using LexFlow.Application.Commands.Matters;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Cases;
using LexFlow.Application.Queries.Matters;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 4 Matter Management (PRD §17).</summary>
[ApiController]
[Route("api/v1/matters")]
public sealed class MattersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateMatterCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpGet]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? matterType, [FromQuery] Guid? practiceAreaId, [FromQuery] Guid? lawyerId, [FromQuery] string? priority, [FromQuery] string? q, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<MatterDto>>.Of(await mediator.Send(new GetMattersQuery(status, matterType, practiceAreaId, lawyerId, priority, q), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterDto>.Of(await mediator.Send(new GetMatterQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMatterRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateMatterCommand(id, request.Title, request.MatterType, request.PracticeAreaId, request.BranchId, request.ResponsibleLawyerId, request.Priority, request.Description, request.Budget, request.IsPrivate, request.BillingArrangementJson);
        return Ok(ApiResponse<MatterDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    /// <summary>AC-M3: closing checklist enforced.</summary>
    [HttpPost("{id:guid}/status")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeMatterStatusRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterDto>.Of(await mediator.Send(new ChangeMatterStatusCommand(id, request.ToStatus, request.Outcome, request.ClosureNote), cancellationToken)));

    [HttpPost("{id:guid}/team")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddTeamMember(Guid id, [FromBody] AddTeamMemberRequest request, CancellationToken cancellationToken)
    {
        await mediator.Send(new AddMatterTeamMemberCommand(id, request.UserId, request.RoleInMatter, request.RateOverride), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/team/{userId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> RemoveTeamMember(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveMatterTeamMemberCommand(id, userId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/team")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetTeam(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<MatterTeamMemberDto>>.Of(await mediator.Send(new GetMatterTeamQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/parties")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddParty(Guid id, [FromBody] MatterPartyRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterPartyDto>.Of(await mediator.Send(new AddMatterPartyCommand(id, request.Name, request.PartyRole, request.AdvocateName, request.ContactJson), cancellationToken)));

    [HttpPut("{id:guid}/parties/{partyId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> UpdateParty(Guid id, Guid partyId, [FromBody] MatterPartyRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterPartyDto>.Of(await mediator.Send(new UpdateMatterPartyCommand(id, partyId, request.Name, request.PartyRole, request.AdvocateName, request.ContactJson), cancellationToken)));

    [HttpDelete("{id:guid}/parties/{partyId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> DeleteParty(Guid id, Guid partyId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteMatterPartyCommand(id, partyId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/parties")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetParties(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<MatterPartyDto>>.Of(await mediator.Send(new GetMatterPartiesQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/important-dates")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddImportantDate(Guid id, [FromBody] ImportantDateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterImportantDateDto>.Of(await mediator.Send(new AddMatterImportantDateCommand(id, request.Kind, request.Title, request.DueAt, request.ReminderPolicyJson, request.Severity), cancellationToken)));

    [HttpPut("{id:guid}/important-dates/{dateId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> UpdateImportantDate(Guid id, Guid dateId, [FromBody] ImportantDateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterImportantDateDto>.Of(await mediator.Send(new UpdateMatterImportantDateCommand(id, dateId, request.Kind, request.Title, request.DueAt, request.ReminderPolicyJson, request.Severity), cancellationToken)));

    [HttpDelete("{id:guid}/important-dates/{dateId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> DeleteImportantDate(Guid id, Guid dateId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteMatterImportantDateCommand(id, dateId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/important-dates")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetImportantDates(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<MatterImportantDateDto>>.Of(await mediator.Send(new GetMatterImportantDatesQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/expenses")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddExpense(Guid id, [FromBody] MatterExpenseRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterExpenseDto>.Of(await mediator.Send(new AddMatterExpenseCommand(id, request.IncurredOn, request.Category, request.Description, request.Amount, request.Billable, request.ReceiptDocumentId), cancellationToken)));

    [HttpGet("{id:guid}/expenses")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetExpenses(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<MatterExpenseDto>>.Of(await mediator.Send(new GetMatterExpensesQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/related")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddRelated(Guid id, [FromBody] MatterRelatedRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterRelatedDto>.Of(await mediator.Send(new AddMatterRelatedCommand(id, request.RelatedMatterId, request.RelationType), cancellationToken)));

    /// <summary>AC-M2: 6-entity-type merged timeline, cursor paged.</summary>
    [HttpGet("{id:guid}/timeline")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetTimeline(Guid id, [FromQuery] string? types, [FromQuery] string? cursor, CancellationToken cancellationToken)
        => Ok(ApiResponse<TimelinePage>.Of(await mediator.Send(new GetMatterTimelineQuery(id, types, cursor), cancellationToken)));

    /// <summary>AC-M4.</summary>
    [HttpGet("{id:guid}/financial-summary")]
    [RequirePermission("billing.read.all")]
    public async Task<IActionResult> GetFinancialSummary(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<MatterFinancialSummaryDto>.Of(await mediator.Send(new GetMatterFinancialSummaryQuery(id), cancellationToken)));

    /// <summary>AC-M1: fires the Elasticsearch fuzzy conflict check.</summary>
    [HttpPost("conflict-check")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> ConflictCheck([FromBody] ConflictCheckRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ConflictMatch>>.Of(await mediator.Send(new ConflictCheckQuery(request.PartyNames), cancellationToken)));

    /// <summary>Module 5: "From a litigation matter, add Court Case" — POST /api/v1/matters/{matterId}/cases.</summary>
    [HttpPost("{matterId:guid}/cases")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> CreateCase(Guid matterId, [FromBody] CreateCourtCaseRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateCourtCaseCommand(matterId, request.CourtId, request.CaseType, request.CaseNumber, request.CaseYear, request.CnrNumber, request.FilingDate, request.Stage, request.JudgeId, request.Courtroom);
        return Ok(ApiResponse<CourtCaseDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpGet("{matterId:guid}/cases")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetCases(Guid matterId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<CourtCaseDto>>.Of(await mediator.Send(new GetCourtCasesByMatterQuery(matterId), cancellationToken)));
}

public sealed record UpdateMatterRequest(string Title, string MatterType, Guid? PracticeAreaId, Guid? BranchId, Guid? ResponsibleLawyerId, string Priority, string? Description, decimal? Budget, bool IsPrivate, string? BillingArrangementJson);

public sealed record ChangeMatterStatusRequest(string ToStatus, string? Outcome, string? ClosureNote);

public sealed record AddTeamMemberRequest(Guid UserId, string? RoleInMatter, decimal? RateOverride);

public sealed record MatterPartyRequest(string Name, string PartyRole, string? AdvocateName, string? ContactJson);

public sealed record ImportantDateRequest(string Kind, string Title, DateTimeOffset DueAt, string? ReminderPolicyJson, string? Severity);

public sealed record MatterExpenseRequest(DateOnly IncurredOn, string? Category, string? Description, decimal Amount, bool Billable, Guid? ReceiptDocumentId);

public sealed record MatterRelatedRequest(Guid RelatedMatterId, string RelationType);

public sealed record ConflictCheckRequest(IReadOnlyList<string> PartyNames);

public sealed record CreateCourtCaseRequest(Guid CourtId, string CaseType, string CaseNumber, int CaseYear, string? CnrNumber, DateOnly? FilingDate, string? Stage, Guid? JudgeId, string? Courtroom);
