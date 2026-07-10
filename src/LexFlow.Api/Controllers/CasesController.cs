using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Cases;
using LexFlow.Application.Commands.Hearings;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Cases;
using LexFlow.Application.Queries.Hearings;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 5 Court Case Management (PRD §17).</summary>
[ApiController]
[Route("api/v1/cases")]
public sealed class CasesController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<CourtCaseDto>.Of(await mediator.Send(new GetCourtCaseQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCourtCaseRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CourtCaseDto>.Of(await mediator.Send(new UpdateCourtCaseCommand(id, request.CnrNumber, request.FilingDate, request.JudgeId, request.Courtroom), cancellationToken)));

    /// <summary>case.stage.update permission distinct from read (PRD Security Rules).</summary>
    [HttpPost("{id:guid}/stage")]
    [RequirePermission("case.stage.update")]
    public async Task<IActionResult> ChangeStage(Guid id, [FromBody] ChangeCaseStageRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CourtCaseDto>.Of(await mediator.Send(new ChangeCourtCaseStageCommand(id, request.ToStage), cancellationToken)));

    /// <summary>AC-CC4.</summary>
    [HttpPost("{id:guid}/appeal")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> FileAppeal(Guid id, [FromBody] FileAppealRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CourtCaseDto>.Of(await mediator.Send(new FileCourtCaseAppealCommand(id, request.TargetCourtId, request.CarryDocumentIds), cancellationToken)));

    [HttpPost("{id:guid}/parties")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddParty(Guid id, [FromBody] CasePartyRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CasePartyDto>.Of(await mediator.Send(new AddCasePartyCommand(id, request.PartyRole, request.Name, request.AdvocateName, request.AdvocateUserId, request.ContactJson), cancellationToken)));

    [HttpPut("{id:guid}/parties/{partyId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> UpdateParty(Guid id, Guid partyId, [FromBody] CasePartyRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CasePartyDto>.Of(await mediator.Send(new UpdateCasePartyCommand(id, partyId, request.PartyRole, request.Name, request.AdvocateName, request.AdvocateUserId, request.ContactJson), cancellationToken)));

    [HttpDelete("{id:guid}/parties/{partyId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> DeleteParty(Guid id, Guid partyId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCasePartyCommand(id, partyId), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/parties")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetParties(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<CasePartyDto>>.Of(await mediator.Send(new GetCasePartiesQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/hearings")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddHearing(Guid id, [FromBody] CreateHearingRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<HearingDto>.Of(await mediator.Send(new CreateHearingCommand(id, request.Date, request.Time, request.Purpose, request.Courtroom, request.AssignedLawyerId), cancellationToken)));

    [HttpGet("{id:guid}/hearings")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetHearings(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<HearingDto>>.Of(await mediator.Send(new GetHearingsByCaseQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/orders")]
    [RequirePermission("matters.manage.all")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> AddOrder(Guid id, [FromForm] AddOrderForm form, CancellationToken cancellationToken)
        => Ok(ApiResponse<CourtOrderDto>.Of(await mediator.Send(new AddCourtOrderCommand(id, form.HearingId, form.OrderDate, form.Gist, form.ComplianceDue, DocumentId: null), cancellationToken)));

    [HttpGet("{id:guid}/orders")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetOrders(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<CourtOrderDto>>.Of(await mediator.Send(new GetCourtOrdersQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/evidence")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddEvidence(Guid id, [FromBody] AddEvidenceRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<EvidenceItemDto>.Of(await mediator.Send(new AddEvidenceItemCommand(id, request.ExhibitNo, request.Kind, request.Description, request.DocumentId), cancellationToken)));

    [HttpGet("{id:guid}/evidence")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetEvidence(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<EvidenceItemDto>>.Of(await mediator.Send(new GetEvidenceItemsQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/witnesses")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddWitness(Guid id, [FromBody] WitnessRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<WitnessDto>.Of(await mediator.Send(new AddWitnessCommand(id, request.Name, request.Side, request.ContactJson, request.ScheduledOn), cancellationToken)));

    [HttpPut("{id:guid}/witnesses/{witnessId:guid}")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> UpdateWitness(Guid id, Guid witnessId, [FromBody] WitnessRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<WitnessDto>.Of(await mediator.Send(new UpdateWitnessCommand(id, witnessId, request.Name, request.Side, request.ContactJson, request.ScheduledOn, request.ExamStatus), cancellationToken)));

    [HttpGet("{id:guid}/witnesses")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetWitnesses(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<WitnessDto>>.Of(await mediator.Send(new GetWitnessesQuery(id), cancellationToken)));

    [HttpPost("{id:guid}/arguments")]
    [RequirePermission("matters.manage.all")]
    public async Task<IActionResult> AddArgument(Guid id, [FromBody] ArgumentNoteRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ArgumentNoteDto>.Of(await mediator.Send(new AddArgumentNoteCommand(id, request.HearingId, request.Stage, request.Body, request.CitationJudgmentIds), cancellationToken)));

    [HttpGet("{id:guid}/arguments")]
    [RequirePermission("matters.read.all")]
    public async Task<IActionResult> GetArguments(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<ArgumentNoteDto>>.Of(await mediator.Send(new GetArgumentNotesQuery(id), cancellationToken)));
}

public sealed record UpdateCourtCaseRequest(string? CnrNumber, DateOnly? FilingDate, Guid? JudgeId, string? Courtroom);

public sealed record ChangeCaseStageRequest(string ToStage);

public sealed record FileAppealRequest(Guid TargetCourtId, IReadOnlyList<Guid>? CarryDocumentIds);

public sealed record CasePartyRequest(string PartyRole, string Name, string? AdvocateName, Guid? AdvocateUserId, string? ContactJson);

public sealed record CreateHearingRequest(DateOnly Date, TimeOnly? Time, string? Purpose, string? Courtroom, Guid? AssignedLawyerId);

public sealed record AddOrderForm(Guid? HearingId, DateOnly OrderDate, string? Gist, DateOnly? ComplianceDue);

public sealed record AddEvidenceRequest(string? ExhibitNo, string Kind, string? Description, Guid? DocumentId);

public sealed record WitnessRequest(string Name, string? Side, string? ContactJson, DateOnly? ScheduledOn, string? ExamStatus);

public sealed record ArgumentNoteRequest(Guid? HearingId, string? Stage, string Body, IReadOnlyList<Guid>? CitationJudgmentIds);
