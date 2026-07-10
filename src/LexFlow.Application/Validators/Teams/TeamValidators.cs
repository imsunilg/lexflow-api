using FluentValidation;
using LexFlow.Application.Commands.Teams;

namespace LexFlow.Application.Validators.Teams;

public sealed class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator() => RuleFor(x => x.Name).NotEmpty();
}

public sealed class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.TeamId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty();
    }
}
