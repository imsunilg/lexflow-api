using LexFlow.Application.Commands.Sessions;
using LexFlow.Infrastructure.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LexFlow.Api.Controllers;

/// <summary>DELETE /api/v1/sessions/{id} (PRD §17, §20(11) session manager). Listing a user's sessions lives on UsersController (GET /users/{id}/sessions).</summary>
[ApiController]
[Route("api/v1/sessions")]
public sealed class SessionsController(IMediator mediator) : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [RequirePermission("users.manage.all")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new RevokeSessionCommand(id), cancellationToken);
        return NoContent();
    }
}
