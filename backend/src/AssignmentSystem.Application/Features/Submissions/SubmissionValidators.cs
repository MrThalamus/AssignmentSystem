using FluentValidation;

namespace AssignmentSystem.Application.Features.Submissions;

// Handing in and revising are not represented here: the payload is a multipart file
// rather than a JSON body, so it never reaches the auto-validator. Those rules live in
// SubmissionFileRules, which the service applies directly.

public class GradeSubmissionRequestValidator : AbstractValidator<GradeSubmissionRequest>
{
    public GradeSubmissionRequestValidator()
    {
        // The upper bound depends on the assignment, so it is enforced in the domain.
        RuleFor(x => x.Marks).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Feedback).MaximumLength(5000);
    }
}

public class ChangeSubmissionStatusRequestValidator : AbstractValidator<ChangeSubmissionStatusRequest>
{
    public ChangeSubmissionStatusRequestValidator() => RuleFor(x => x.Status).IsInEnum();
}
