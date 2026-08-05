using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Users;

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt);

public record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    UserRole Role);

/// <summary>
/// Role is deliberately not editable: moving an account between roles would strand
/// its enrollments or teaching assignments. Deactivate and create instead.
/// </summary>
public record UpdateUserRequest(string FullName, string Email, bool IsActive);

public record ResetPasswordRequest(string NewPassword);

public class UserListQuery : PaginationQuery
{
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>Case-insensitive match on name or email.</summary>
    public string? Search { get; set; }
}
