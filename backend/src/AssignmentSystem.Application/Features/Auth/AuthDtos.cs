using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Auth;

public record LoginRequest(string Email, string Password);

public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, AuthenticatedUserDto User);

public record AuthenticatedUserDto(Guid Id, string FullName, string Email, UserRole Role);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
