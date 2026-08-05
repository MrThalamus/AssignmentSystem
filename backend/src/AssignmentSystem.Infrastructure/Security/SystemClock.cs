using AssignmentSystem.Application.Common.Interfaces;

namespace AssignmentSystem.Infrastructure.Security;

public class SystemClock : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
