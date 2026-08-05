using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Application.Features.Auth;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthenticatedUserDto> GetCurrentUserAsync(CancellationToken ct = default);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);
}

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public AuthService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        // Same message whether the account is missing, the password is wrong, or the
        // account is disabled - otherwise the endpoint doubles as an account probe.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
            throw new ForbiddenException("Invalid email or password.");

        if (!user.IsActive)
            throw new ForbiddenException("Invalid email or password.");

        var (token, expiresAt) = _tokenGenerator.Generate(user);

        return new AuthResponse(
            token,
            expiresAt,
            new AuthenticatedUserDto(user.Id, user.FullName, user.Email, user.Role));
    }

    public async Task<AuthenticatedUserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("The signed-in account no longer exists.");

        return new AuthenticatedUserDto(user.Id, user.FullName, user.Email, user.Role);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.RequireUserId();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new NotFoundException("The signed-in account no longer exists.");

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException(nameof(request.CurrentPassword), "The current password is incorrect.");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
    }
}
