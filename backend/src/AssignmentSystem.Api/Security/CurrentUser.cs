using System.IdentityModel.Tokens.Jwt;
using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Security;

namespace AssignmentSystem.Api.Security;

/// <summary>
/// Reads the caller's identity from the validated JWT on the current request.
/// Services depend on this rather than on ids in the request body, so a caller
/// cannot act as another user by editing what they send.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public Guid? UserId =>
        Guid.TryParse(FindClaim(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public UserRole? Role =>
        Enum.TryParse<UserRole>(FindClaim(JwtTokenGenerator.RoleClaim), out var role) ? role : null;

    public bool IsAuthenticated => _accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public Guid RequireUserId() =>
        UserId ?? throw new ForbiddenException("The request is not authenticated.");

    private string? FindClaim(string type) => _accessor.HttpContext?.User.FindFirst(type)?.Value;
}
