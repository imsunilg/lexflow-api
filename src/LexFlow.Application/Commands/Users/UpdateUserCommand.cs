using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Users;

/// <summary>PUT /api/v1/users/{id} (PRD §17, Module 14).</summary>
public sealed record UpdateUserCommand(
    Guid UserId,
    string Name,
    string? Designation,
    string? BarEnrollmentNo,
    string? Phone,
    decimal? CostRate,
    Guid? BranchId,
    Guid? DepartmentId,
    string? Tz,
    string? Locale,
    string NotificationPrefsJson) : IRequest<UserDto>;

public sealed class UpdateUserCommandHandler(IUserManagementService userManagementService, ICurrentUserService currentUser)
    : IRequestHandler<UpdateUserCommand, UserDto>
{
    public Task<UserDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var input = new UpdateUserProfileInput(
            request.Name,
            request.Designation,
            request.BarEnrollmentNo,
            request.Phone,
            request.CostRate,
            request.BranchId,
            request.DepartmentId,
            request.Tz,
            request.Locale,
            request.NotificationPrefsJson);

        return userManagementService.UpdateAsync(currentUser.TenantId!.Value, request.UserId, input, cancellationToken);
    }
}
