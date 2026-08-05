namespace AssignmentSystem.Api.Security;

/// <summary>
/// Role names as they appear in the JWT. Constants rather than literals so a typo in
/// an <c>[Authorize]</c> attribute - which would silently deny everyone - becomes a
/// compile error instead.
/// </summary>
public static class Roles
{
    public const string Admin = nameof(Domain.Enums.UserRole.Admin);
    public const string Teacher = nameof(Domain.Enums.UserRole.Teacher);
    public const string Student = nameof(Domain.Enums.UserRole.Student);

    public const string AdminOrTeacher = $"{Admin},{Teacher}";
    public const string TeacherOrStudent = $"{Teacher},{Student}";
}
