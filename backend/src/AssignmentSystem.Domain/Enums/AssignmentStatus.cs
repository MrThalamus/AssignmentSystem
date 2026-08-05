namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Lifecycle of an assignment.
/// <list type="bullet">
/// <item><see cref="Draft"/> - visible to the owning teacher and admins only.</item>
/// <item><see cref="Published"/> - visible to enrolled students; accepts submissions.</item>
/// <item><see cref="Closed"/> - visible to students but no longer accepts submissions.</item>
/// </list>
/// </summary>
public enum AssignmentStatus
{
    Draft = 1,
    Published = 2,
    Closed = 3
}
