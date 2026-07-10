using FluentValidation;
using LexFlow.Application.Commands.Users;

namespace LexFlow.Application.Validators.Users;

public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.RoleId).NotEmpty();
    }
}

public sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
        RuleFor(x => x.CostRate).GreaterThanOrEqualTo(0).When(x => x.CostRate.HasValue);
    }
}

public sealed class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleForEach(x => x.Reassignments).ChildRules(r =>
        {
            r.RuleFor(e => e.EntityType).NotEmpty();
            r.RuleFor(e => e.NewAssigneeUserId).NotEmpty();
        });
    }
}
