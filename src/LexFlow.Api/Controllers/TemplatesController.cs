using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Templates;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Templates;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 15 §11 Email/SMS/WhatsApp Templates CRUD (PRD §17: "CRUD number-series, tax-rates, templates").</summary>
[ApiController]
[Route("api/v1/settings/templates")]
public sealed class TemplatesController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [RequirePermission("settings.read.all")]
    public async Task<IActionResult> GetAll([FromQuery] string? channel, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<CommTemplateDto>>.Of(await mediator.Send(new GetCommTemplatesQuery(channel), cancellationToken)));

    [HttpPost]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateCommTemplateCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<CommTemplateDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCommTemplateRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<CommTemplateDto>.Of(await mediator.Send(new UpdateCommTemplateCommand(id, request.Body, request.VariablesJson, request.IsActive), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("settings.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteCommTemplateCommand(id), cancellationToken);
        return NoContent();
    }
}

public sealed record UpdateCommTemplateRequest(string Body, string VariablesJson, bool IsActive);
