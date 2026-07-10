using FluentValidation;
using LexFlow.Application.Commands.Departments;

namespace LexFlow.Application.Validators.Departments;

public sealed class CreateDepartmentCommandValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentCommandValidator() => RuleFor(x => x.Name).NotEmpty();
}

public sealed class UpdateDepartmentCommandValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentCommandValidator()
    {
        RuleFor(x => x.DepartmentId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}
