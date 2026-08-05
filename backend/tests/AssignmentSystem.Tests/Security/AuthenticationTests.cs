using System.IdentityModel.Tokens.Jwt;
using AssignmentSystem.Application.Common.Exceptions;
using AssignmentSystem.Application.Features.Auth;
using AssignmentSystem.Domain.Entities;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Security;
using AssignmentSystem.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace AssignmentSystem.Tests.Security;

public class AuthenticationTests
{
    private const string SigningKey = "test-signing-key-that-is-long-enough-for-hmac-sha256";

    private static JwtTokenGenerator TokenGenerator(FakeClock clock) =>
        new(Options.Create(new JwtSettings
        {
            Issuer = "AssignmentSystem",
            Audience = "AssignmentSystemClient",
            Key = SigningKey,
            ExpiryMinutes = 60
        }), clock);

    private static (AuthService Service, User Account) BuildAuthService(
        TestWorld world, string password = "Password1", bool isActive = true)
    {
        var hasher = new Pbkdf2PasswordHasher();

        var account = new User
        {
            FullName = "Login Test",
            Email = "login.test@test.edu",
            PasswordHash = hasher.Hash(password),
            Role = UserRole.Teacher,
            IsActive = isActive,
            CreatedAt = world.Clock.UtcNow
        };

        world.Db.Users.Add(account);
        world.Db.SaveChanges();

        var service = new AuthService(
            world.Db, hasher, TokenGenerator(world.Clock), world.CurrentUser, world.Clock);

        return (service, account);
    }

    // ----------------------------------------------------------------- login

    [Fact]
    public async Task Correct_credentials_return_a_token_and_the_account_details()
    {
        using var world = new TestWorld();
        var (auth, account) = BuildAuthService(world);

        var response = await auth.LoginAsync(new LoginRequest("login.test@test.edu", "Password1"));

        Assert.NotEmpty(response.AccessToken);
        Assert.Equal(account.Id, response.User.Id);
        Assert.Equal(UserRole.Teacher, response.User.Role);
        Assert.Equal(world.Clock.UtcNow.AddMinutes(60), response.ExpiresAtUtc);
    }

    [Fact]
    public async Task The_email_is_matched_case_insensitively()
    {
        using var world = new TestWorld();
        var (auth, _) = BuildAuthService(world);

        var response = await auth.LoginAsync(new LoginRequest("  Login.Test@TEST.edu ", "Password1"));

        Assert.NotEmpty(response.AccessToken);
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        using var world = new TestWorld();
        var (auth, _) = BuildAuthService(world);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => auth.LoginAsync(new LoginRequest("login.test@test.edu", "WrongPassword1")));
    }

    [Fact]
    public async Task A_deactivated_account_cannot_log_in()
    {
        using var world = new TestWorld();
        var (auth, _) = BuildAuthService(world, isActive: false);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => auth.LoginAsync(new LoginRequest("login.test@test.edu", "Password1")));
    }

    [Fact]
    public async Task An_unknown_account_and_a_wrong_password_fail_identically()
    {
        // Distinguishing the two would turn the login endpoint into a way to find out
        // which email addresses are registered.
        using var world = new TestWorld();
        var (auth, _) = BuildAuthService(world);

        var unknown = await Assert.ThrowsAsync<ForbiddenException>(
            () => auth.LoginAsync(new LoginRequest("nobody@test.edu", "Password1")));

        var wrongPassword = await Assert.ThrowsAsync<ForbiddenException>(
            () => auth.LoginAsync(new LoginRequest("login.test@test.edu", "WrongPassword1")));

        Assert.Equal(unknown.Message, wrongPassword.Message);
    }

    // ------------------------------------------------------- password change

    [Fact]
    public async Task Changing_a_password_requires_the_current_one()
    {
        using var world = new TestWorld();
        var (auth, account) = BuildAuthService(world);
        world.CurrentUser.SignInAs(account.Id, UserRole.Teacher);

        await Assert.ThrowsAsync<ValidationException>(
            () => auth.ChangePasswordAsync(new ChangePasswordRequest("NotMyPassword1", "NewPassword1")));
    }

    [Fact]
    public async Task A_changed_password_is_the_one_that_works_afterwards()
    {
        using var world = new TestWorld();
        var (auth, account) = BuildAuthService(world);
        world.CurrentUser.SignInAs(account.Id, UserRole.Teacher);

        await auth.ChangePasswordAsync(new ChangePasswordRequest("Password1", "BrandNew2"));

        await Assert.ThrowsAsync<ForbiddenException>(
            () => auth.LoginAsync(new LoginRequest("login.test@test.edu", "Password1")));

        var response = await auth.LoginAsync(new LoginRequest("login.test@test.edu", "BrandNew2"));
        Assert.NotEmpty(response.AccessToken);
    }

    // ------------------------------------------------------------- the token

    [Fact]
    public void The_token_carries_the_id_and_role_the_api_authorises_on()
    {
        var clock = new FakeClock();
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Token Subject",
            Email = "token@test.edu",
            Role = UserRole.Admin
        };

        var (token, expiresAt) = TokenGenerator(clock).Generate(user);
        var decoded = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), decoded.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal("Admin", decoded.Claims.Single(c => c.Type == JwtTokenGenerator.RoleClaim).Value);
        Assert.Equal("token@test.edu", decoded.Claims.Single(c => c.Type == JwtTokenGenerator.EmailClaim).Value);
        Assert.Equal("AssignmentSystem", decoded.Issuer);
        Assert.Equal(clock.UtcNow.AddMinutes(60), expiresAt);
    }

    [Fact]
    public void Each_token_gets_its_own_identifier()
    {
        var clock = new FakeClock();
        var user = new User { Id = Guid.NewGuid(), Email = "token@test.edu", Role = UserRole.Student };
        var generator = TokenGenerator(clock);

        var first = new JwtSecurityTokenHandler().ReadJwtToken(generator.Generate(user).Token);
        var second = new JwtSecurityTokenHandler().ReadJwtToken(generator.Generate(user).Token);

        Assert.NotEqual(
            first.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value,
            second.Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value);
    }
}
