using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Leads;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Leads;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 2 Lead Management (PRD §17).</summary>
[ApiController]
[Route("api/v1/leads")]
public sealed class LeadsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("leads.create.all")]
    public async Task<IActionResult> Create([FromBody] CreateLeadCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<LeadDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpGet]
    [RequirePermission("leads.read.all")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? stage, [FromQuery] Guid? source, [FromQuery] Guid? owner, [FromQuery] int? score,
        [FromQuery] DateTimeOffset? createdFrom, [FromQuery] DateTimeOffset? createdTo, [FromQuery] string? q, [FromQuery] string? status,
        CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<LeadDto>>.Of(await mediator.Send(new GetLeadsQuery(stage, source, owner, score, createdFrom, createdTo, q, status), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("leads.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<LeadDto>.Of(await mediator.Send(new GetLeadQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeadRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateLeadCommand(id, request.FirstName, request.LastName, request.Company, request.Email, request.PhoneE164, request.SourceId, request.PracticeAreaId, request.IssueSummary, request.OpposingParty, request.BudgetBand);
        return Ok(ApiResponse<LeadDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteLeadCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>AC-L2: writes lead_stage_history and notifies owner.</summary>
    [HttpPost("{id:guid}/stage")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> ChangeStage(Guid id, [FromBody] ChangeLeadStageRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<LeadDto>.Of(await mediator.Send(new ChangeLeadStageCommand(id, request.ToStage, request.Note), cancellationToken)));

    [HttpPost("{id:guid}/activities")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> AddActivity(Guid id, [FromBody] AddLeadActivityRequest request, CancellationToken cancellationToken)
    {
        var command = new AddLeadActivityCommand(id, request.Type, request.Direction, request.DurationMin, request.Subject, request.Body, request.Outcome);
        return Ok(ApiResponse<LeadActivityDto>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpPost("{id:guid}/assign")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignLeadRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<LeadDto>.Of(await mediator.Send(new AssignLeadCommand(id, request.UserId, request.RuleId), cancellationToken)));

    /// <summary>AC-L3: atomic Client(+Matter+Invoice) conversion.</summary>
    [HttpPost("{id:guid}/convert")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> Convert(Guid id, [FromBody] ConvertLeadRequest request, CancellationToken cancellationToken)
    {
        var command = new ConvertLeadCommand(id, request.CreateMatter, request.MatterPayload, request.InvoicePayload);
        return Ok(ApiResponse<ConvertLeadResult>.Of(await mediator.Send(command, cancellationToken)));
    }

    [HttpPost("{id:guid}/lost")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> MarkLost(Guid id, [FromBody] MarkLeadLostRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<LeadDto>.Of(await mediator.Send(new MarkLeadLostCommand(id, request.ReasonId, request.Note), cancellationToken)));

    /// <summary>Module 2 User Flow step 2: phone/email exact + pg_trgm fuzzy name match.</summary>
    [HttpPost("check-duplicates")]
    [RequirePermission("leads.create.all")]
    public async Task<IActionResult> CheckDuplicates([FromBody] CheckDuplicatesRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<DuplicateMatchDto>>.Of(await mediator.Send(new CheckLeadDuplicatesQuery(request.Name, request.Email, request.PhoneE164), cancellationToken)));

    /// <summary>AC-L5: 10k-row CSV/XLSX import via background job.</summary>
    [HttpPost("import")]
    [RequirePermission("leads.manage.all")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Import(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);
        return Ok(ApiResponse<LeadImportBatchDto>.Of(await mediator.Send(new ImportLeadsCommand(file.FileName, stream.ToArray()), cancellationToken)));
    }

    [HttpGet("import/{batchId:guid}")]
    [RequirePermission("leads.manage.all")]
    public async Task<IActionResult> GetImportBatch(Guid batchId, CancellationToken cancellationToken)
        => Ok(ApiResponse<LeadImportBatchDto>.Of(await mediator.Send(new GetLeadImportBatchQuery(batchId), cancellationToken)));

    [HttpGet("export")]
    [RequirePermission("leads.export.all")]
    public async Task<IActionResult> Export([FromQuery] string? stage, [FromQuery] Guid? source, [FromQuery] Guid? owner, [FromQuery] string format = "csv", CancellationToken cancellationToken = default)
    {
        var content = await mediator.Send(new ExportLeadsQuery(stage, source, owner, format), cancellationToken);
        var contentType = format.Equals("xlsx", StringComparison.OrdinalIgnoreCase) ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/csv";
        return File(content, contentType, $"leads-export.{format}");
    }
}

public sealed record UpdateLeadRequest(string FirstName, string? LastName, string? Company, string? Email, string? PhoneE164, Guid? SourceId, Guid? PracticeAreaId, string? IssueSummary, string? OpposingParty, string? BudgetBand);

public sealed record ChangeLeadStageRequest(string ToStage, string? Note);

public sealed record AddLeadActivityRequest(string Type, string? Direction, int? DurationMin, string? Subject, string? Body, string? Outcome);

public sealed record AssignLeadRequest(Guid? UserId, Guid? RuleId);

public sealed record ConvertLeadRequest(bool CreateMatter, string? MatterPayload, string? InvoicePayload);

public sealed record MarkLeadLostRequest(Guid ReasonId, string? Note);

public sealed record CheckDuplicatesRequest(string? Name, string? Email, string? PhoneE164);
