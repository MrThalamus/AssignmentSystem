using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Application.Common.Models;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Application.Features.Users;

public interface IUserService
{
    Task<PagedResult<UserDto>> ListAsync(UserListQuery query, CancellationToken ct = default);
    Task<UserDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Admin-only account management. Role enforcement itself sits on the controller;
/// what this class adds is the data-level rules an attribute cannot express.
/// </summary>
public class UserService : IUserService
{
    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _clock;

    public UserService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IDateTimeProvider clock)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<PagedResult<UserDto>> ListAsync(UserListQuery query, CancellationToken ct = default)
    {
        var users = _db.Users.AsNoTracking();

        if (query.Role is { } role)
            users = users.Where(u => u.Role == role);

        if (query.IsActive is { } isActive)
            users = users.Where(u => u.IsActive == isActive);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            users = users.Where(u => u.FullName.ToLower().Contains(term) || u.Email.Contains(term));
        }

        return await users
            .OrderBy(u => u.FullName)
            .Select(u => new UserDto(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw NotFoundException.For("User", id);

        return Map(user);
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException($"An account with the email '{email}' already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = request.Role,
            IsActive = true,
            CreatedAt = _clock.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return Map(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw NotFoundException.For("User", id);

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email && u.Id != id, ct))
            throw new ConflictException($"An account with the email '{email}' already exists.");

        // An admin locking themselves out would leave the system unmanageable.
        if (!request.IsActive && user.Id == _currentUser.UserId)
            throw new ConflictException("You cannot deactivate your own account.");

        user.FullName = request.FullName.Trim();
        user.Email = email;
        user.IsActive = request.IsActive;
        user.UpdatedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Map(user);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw NotFoundException.For("User", id);

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Accounts are deactivated rather than deleted. Assignments, submissions and
    /// marks reference their author, so removing the row would destroy academic
    /// history that other people still need to read.
    /// </summary>
    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw NotFoundException.For("User", id);

        if (user.Id == _currentUser.UserId)
            throw new ConflictException("You cannot deactivate your own account.");

        if (user.Role == UserRole.Admin
            && await _db.Users.CountAsync(u => u.Role == UserRole.Admin && u.IsActive, ct) <= 1)
            throw new ConflictException("The last active administrator cannot be deactivated.");

        user.IsActive = false;
        user.UpdatedAt = _clock.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    private static UserDto Map(User u) =>
        new(u.Id, u.FullName, u.Email, u.Role, u.IsActive, u.CreatedAt);
}
