using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Commands.Departments;

/// <summary>CRUD /api/v1/departments (PRD §17).</summary>
public sealed record CreateDepartmentCommand(string Name, string? Code, Guid? HeadUserId) : IRequest<DepartmentDto>;

public sealed class CreateDepartmentCommandHandler(IDepartmentService departmentService, ICurrentUserService currentUser)
    : IRequestHandler<CreateDepartmentCommand, DepartmentDto>
{
    public Task<DepartmentDto> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
        => departmentService.CreateAsync(currentUser.TenantId!.Value, request.Name, request.Code, request.HeadUserId, cancellationToken);
}

public sealed record UpdateDepartmentCommand(Guid DepartmentId, string Name, string? Code, Guid? HeadUserId) : IRequest<DepartmentDto>;

public sealed class UpdateDepartmentCommandHandler(IDepartmentService departmentService, ICurrentUserService currentUser)
    : IRequestHandler<UpdateDepartmentCommand, DepartmentDto>
{
    public Task<DepartmentDto> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
        => departmentService.UpdateAsync(currentUser.TenantId!.Value, request.DepartmentId, request.Name, request.Code, request.HeadUserId, cancellationToken);
}

public sealed record DeleteDepartmentCommand(Guid DepartmentId) : IRequest;

public sealed class DeleteDepartmentCommandHandler(IDepartmentService departmentService, ICurrentUserService currentUser) : IRequestHandler<DeleteDepartmentCommand>
{
    public async Task Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
        => await departmentService.DeleteAsync(currentUser.TenantId!.Value, request.DepartmentId, cancellationToken);
}
