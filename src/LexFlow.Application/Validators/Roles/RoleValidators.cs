using FluentValidation;
using LexFlow.Application.Commands.Roles;

namespace LexFlow.Application.Validators.Roles;

public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Key).NotEmpty().Matches("^[a-z][a-z0-9_]*$").WithMessage("Role key must be lower_snake_case.");
        RuleFor(x => x.Name).NotEmpty();
    }
}

public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.RoleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}
