using FluentValidation;

namespace AssignmentSystem.Application.Common.Validation;

public static class PasswordRules
{
    public const int MinimumLength = 8;

    /// <summary>
    /// Applied everywhere a password is set so the policy cannot drift between
    /// account creation, admin resets and self-service changes.
    /// </summary>
    public static IRuleBuilderOptions<T, string> Password<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("A password is required.")
            .MinimumLength(MinimumLength)
            .WithMessage($"The password must be at least {MinimumLength} characters long.")
            .Matches("[A-Za-z]").WithMessage("The password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("The password must contain at least one digit.");
}
