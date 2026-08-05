using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Common.Interfaces;

/// <summary>
/// Wraps <see cref="DateTime.UtcNow"/> so deadline behaviour can be exercised
/// deterministically in tests instead of depending on the wall clock.
/// </summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>Hashes and verifies account passwords.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

/// <summary>Issues signed JWT access tokens.</summary>
public interface IJwtTokenGenerator
{
    /// <summary>Returns the encoded token and the instant it expires.</summary>
    (string Token, DateTime ExpiresAtUtc) Generate(User user);
}

/// <summary>
/// The authenticated caller, resolved from the JWT on the current request.
/// Services take identity from here rather than from client-supplied ids, so a
/// caller cannot act as somebody else by editing a request body.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }

    /// <summary>The caller's id, or throws if the request is anonymous.</summary>
    Guid RequireUserId();
}
