using FluentValidation;
using LexFlow.Application.Commands.Tasks;

namespace LexFlow.Application.Validators.Tasks;

public sealed class CreateOpsTaskCommandValidator : AbstractValidator<CreateOpsTaskCommand>
{
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Urgent"];
    private static readonly string[] Categories = ["Filing", "Drafting", "Research", "Compliance", "Follow-up", "Admin"];

    public CreateOpsTaskCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(3, 300);
        RuleFor(x => x.Priority).NotEmpty().Must(p => Priorities.Contains(p)).WithMessage($"Priority must be one of {string.Join('|', Priorities)}.");
        RuleFor(x => x.Category).Must(c => c is null || Categories.Contains(c)).WithMessage($"Category must be one of {string.Join('|', Categories)}.");
    }
}

public sealed class UpdateOpsTaskCommandValidator : AbstractValidator<UpdateOpsTaskCommand>
{
    private static readonly string[] Priorities = ["Low", "Medium", "High", "Urgent"];

    public UpdateOpsTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().Length(3, 300);
        RuleFor(x => x.Priority).NotEmpty().Must(p => Priorities.Contains(p)).WithMessage($"Priority must be one of {string.Join('|', Priorities)}.");
    }
}

public sealed class SetOpsTaskStatusCommandValidator : AbstractValidator<SetOpsTaskStatusCommand>
{
    private static readonly string[] Statuses = ["New", "InProgress", "InReview", "Done", "Cancelled"];

    public SetOpsTaskStatusCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Status).NotEmpty().Must(s => Statuses.Contains(s)).WithMessage($"Status must be one of {string.Join('|', Statuses)}.");
    }
}

public sealed class AddTaskAssigneeCommandValidator : AbstractValidator<AddTaskAssigneeCommand>
{
    private static readonly string[] Roles = ["owner", "collaborator", "watcher"];

    public AddTaskAssigneeCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().Must(r => Roles.Contains(r)).WithMessage($"Role must be one of {string.Join('|', Roles)}.");
    }
}

public sealed class AddTaskChecklistItemCommandValidator : AbstractValidator<AddTaskChecklistItemCommand>
{
    public AddTaskChecklistItemCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(300);
    }
}

public sealed class AddTaskCommentCommandValidator : AbstractValidator<AddTaskCommentCommand>
{
    public AddTaskCommentCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(10_000);
    }
}

/// <summary>AC-TK2: dependency graph must stay acyclic; a self-reference is rejected here before ever reaching the DB trigger.</summary>
public sealed class AddTaskDependencyCommandValidator : AbstractValidator<AddTaskDependencyCommand>
{
    public AddTaskDependencyCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.DependsOnTaskId).NotEmpty();
        RuleFor(x => x).Must(x => x.TaskId != x.DependsOnTaskId).WithMessage("A task cannot depend on itself.");
    }
}

public sealed class ParseTaskTextCommandValidator : AbstractValidator<ParseTaskTextCommand>
{
    public ParseTaskTextCommandValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(1000);
    }
}

public sealed class CreateTaskTemplateCommandValidator : AbstractValidator<CreateTaskTemplateCommand>
{
    public CreateTaskTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(3, 200);
        RuleFor(x => x.Items).Must(i => i.Count <= 100).WithMessage("A checklist/template cannot exceed 100 items.");
    }
}

public sealed class ApplyTaskTemplateCommandValidator : AbstractValidator<ApplyTaskTemplateCommand>
{
    public ApplyTaskTemplateCommandValidator()
    {
        RuleFor(x => x.MatterId).NotEmpty();
        RuleFor(x => x.TemplateId).NotEmpty();
    }
}
