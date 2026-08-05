using FluentValidation;

namespace AssignmentSystem.Application.Features.Assignments;

public class CreateAssignmentRequestValidator : AbstractValidator<CreateAssignmentRequest>
{
    public CreateAssignmentRequestValidator()
    {
        RuleFor(x => x.CourseSubjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(10_000);
        RuleFor(x => x.MaxMarks).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.Deadline).NotEmpty();

        // A draft may carry a placeholder deadline, but publishing immediately means
        // students see it right away - so it has to be reachable.
        RuleFor(x => x.Deadline)
            .Must(d => d.ToUniversalTime() > DateTime.UtcNow)
            .When(x => x.PublishNow)
            .WithMessage("The deadline must be in the future when publishing an assignment.");
    }
}

public class UpdateAssignmentRequestValidator : AbstractValidator<UpdateAssignmentRequest>
{
    public UpdateAssignmentRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(10_000);
        RuleFor(x => x.MaxMarks).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(x => x.Deadline).NotEmpty();
    }
}
