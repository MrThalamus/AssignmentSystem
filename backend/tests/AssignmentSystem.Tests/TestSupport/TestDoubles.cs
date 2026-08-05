using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Tests.TestSupport;

/// <summary>
/// A clock the test drives by hand. Deadline behaviour is the heart of this system,
/// so tests move time explicitly rather than sleeping or depending on when they run.
/// </summary>
public class FakeClock : IDateTimeProvider
{
    public FakeClock(DateTime? startingAt = null) =>
        UtcNow = startingAt ?? new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

/// <summary>Stands in for the JWT-derived caller so tests can switch identity freely.</summary>
public class StubCurrentUser : ICurrentUser
{
    public Guid? UserId { get; set; }
    public UserRole? Role { get; set; }

    public bool IsAuthenticated => UserId is not null;

    public Guid RequireUserId() =>
        UserId ?? throw new ForbiddenException("The request is not authenticated.");

    public StubCurrentUser SignInAs(Guid userId, UserRole role)
    {
        UserId = userId;
        Role = role;
        return this;
    }

    public StubCurrentUser SignOut()
    {
        UserId = null;
        Role = null;
        return this;
    }
}
