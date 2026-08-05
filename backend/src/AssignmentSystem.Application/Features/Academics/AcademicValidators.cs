using FluentValidation;

namespace AssignmentSystem.Application.Features.Academics;

public class SubjectRequestValidator : AbstractValidator<SubjectRequest>
{
    public SubjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30)
            .Matches("^[A-Za-z0-9-]+$")
            .WithMessage("The code may contain letters, digits and hyphens only.");
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class CourseRequestValidator : AbstractValidator<CourseRequest>
{
    public CourseRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(30)
            .Matches("^[A-Za-z0-9-]+$")
            .WithMessage("The code may contain letters, digits and hyphens only.");
        RuleFor(x => x.AcademicYear).NotEmpty().MaximumLength(20);
    }
}

public class AddCourseSubjectRequestValidator : AbstractValidator<AddCourseSubjectRequest>
{
    public AddCourseSubjectRequestValidator() => RuleFor(x => x.SubjectId).NotEmpty();
}

public class EnrollStudentsRequestValidator : AbstractValidator<EnrollStudentsRequest>
{
    public EnrollStudentsRequestValidator()
    {
        RuleFor(x => x.StudentIds).NotEmpty().WithMessage("At least one student must be selected.");
        RuleForEach(x => x.StudentIds).NotEmpty();
    }
}
