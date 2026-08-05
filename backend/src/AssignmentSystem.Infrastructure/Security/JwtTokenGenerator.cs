using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AssignmentSystem.Infrastructure.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    /// <summary>
    /// Claim names are emitted verbatim (inbound mapping is switched off in
    /// <c>Program.cs</c>), so these constants are the single source of truth for
    /// both issuing and reading a token.
    /// </summary>
    public const string RoleClaim = "role";
    public const string NameClaim = "name";
    public const string EmailClaim = "email";

    private readonly JwtSettings _settings;
    private readonly IDateTimeProvider _clock;

    public JwtTokenGenerator(IOptions<JwtSettings> settings, IDateTimeProvider clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public (string Token, DateTime ExpiresAtUtc) Generate(User user)
    {
        var now = _clock.UtcNow;
        var expiresAt = now.AddMinutes(_settings.ExpiryMinutes);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(EmailClaim, user.Email),
            new Claim(NameClaim, user.FullName),
            new Claim(RoleClaim, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now,
            expires: expiresAt,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
