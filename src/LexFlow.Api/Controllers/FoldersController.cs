using LexFlow.Api.Contracts;
using LexFlow.Application.Commands.Folders;
using LexFlow.Application.Common.Interfaces;
using LexFlow.Application.Queries.Folders;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>Module 7 folder tree (PRD §17).</summary>
[ApiController]
[Route("api/v1/folders")]
public sealed class FoldersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Create([FromBody] CreateFolderCommand command, CancellationToken cancellationToken)
        => Ok(ApiResponse<FolderDto>.Of(await mediator.Send(command, cancellationToken)));

    [HttpGet]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> GetAll([FromQuery] Guid? parentId, [FromQuery] Guid? matterId, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyList<FolderDto>>.Of(await mediator.Send(new GetFoldersQuery(parentId, matterId), cancellationToken)));

    [HttpGet("{id:guid}")]
    [RequirePermission("documents.read.all")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(ApiResponse<FolderDto>.Of(await mediator.Send(new GetFolderQuery(id), cancellationToken)));

    [HttpPut("{id:guid}")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateFolderRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<FolderDto>.Of(await mediator.Send(new UpdateFolderCommand(id, request.Name), cancellationToken)));

    [HttpDelete("{id:guid}")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteFolderCommand(id), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/move")]
    [RequirePermission("documents.manage.all")]
    public async Task<IActionResult> Move(Guid id, [FromBody] MoveFolderRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<FolderDto>.Of(await mediator.Send(new MoveFolderCommand(id, request.NewParentId), cancellationToken)));
}

public sealed record UpdateFolderRequest(string Name);

public sealed record MoveFolderRequest(Guid? NewParentId);
