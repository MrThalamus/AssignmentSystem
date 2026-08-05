namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Roles supported by the system. Stored as a string in the database so the
/// values stay readable in raw SQL and survive re-ordering of the enum.
/// </summary>
public enum UserRole
{
    Admin = 1,
    Teacher = 2,
    Student = 3
}
