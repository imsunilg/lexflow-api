using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Users;

/// <summary>POST /api/v1/users/invite (PRD §17, Module 14).</summary>
public sealed record InviteUserCommand(string Email, string Name, Guid RoleId, Guid? BranchId, Guid? DepartmentId) : IRequest<UserDto>;

public sealed class InviteUserCommandHandler(IUserManagementService userManagementService, ICurrentUserService currentUser)
    : IRequestHandler<InviteUserCommand, UserDto>
{
    public Task<UserDto> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId!.Value;
        var invitedBy = currentUser.UserId!.Value;

        return userManagementService.InviteAsync(
            tenantId,
            invitedBy,
            new InviteUserInput(request.Email, request.Name, request.RoleId, request.BranchId, request.DepartmentId),
            cancellationToken);
    }
}
