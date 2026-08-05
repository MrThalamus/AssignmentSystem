using FluentValidation;

namespace AssignmentSystem.Application.Features.Submissions;

public class CreateSubmissionRequestValidator : AbstractValidator<CreateSubmissionRequest>
{
    public CreateSubmissionRequestValidator()
    {
        RuleFor(x => x.AssignmentId).NotEmpty();
        RuleFor(x => x.Content).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.AttachmentUrl).AttachmentUrl();
    }
}

public class UpdateSubmissionRequestValidator : AbstractValidator<UpdateSubmissionRequest>
{
    public UpdateSubmissionRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(20_000);
        RuleFor(x => x.AttachmentUrl).AttachmentUrl();
    }
}

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

internal static class SubmissionRules
{
    /// <summary>
    /// Attachments are links rather than uploads, so the value has to be an absolute
    /// http(s) URL - a relative path would render as a broken link for the teacher.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> AttachmentUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(2000)
            .Must(url => string.IsNullOrWhiteSpace(url)
                         || (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                             && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)))
            .WithMessage("The attachment must be an absolute http or https URL.");
}
