using LexFlow.Application.Common.Exceptions;
using LexFlow.Application.Common.Interfaces;
using MediatR;

namespace LexFlow.Application.Queries.Departments;

public sealed record GetDepartmentsQuery : IRequest<IReadOnlyList<DepartmentDto>>;

public sealed class GetDepartmentsQueryHandler(IDepartmentService departmentService, ICurrentUserService currentUser)
    : IRequestHandler<GetDepartmentsQuery, IReadOnlyList<DepartmentDto>>
{
    public Task<IReadOnlyList<DepartmentDto>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
        => departmentService.GetAllAsync(currentUser.TenantId!.Value, cancellationToken);
}

public sealed record GetDepartmentQuery(Guid DepartmentId) : IRequest<DepartmentDto>;

public sealed class GetDepartmentQueryHandler(IDepartmentService departmentService, ICurrentUserService currentUser) : IRequestHandler<GetDepartmentQuery, DepartmentDto>
{
    public async Task<DepartmentDto> Handle(GetDepartmentQuery request, CancellationToken cancellationToken)
        => await departmentService.GetByIdAsync(currentUser.TenantId!.Value, request.DepartmentId, cancellationToken)
           ?? throw new NotFoundException("Department", request.DepartmentId);
}
