namespace AssignmentSystem.Infrastructure.Security;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AssignmentSystem";
    public string Audience { get; set; } = "AssignmentSystemClient";

    /// <summary>
    /// HMAC signing key. Supplied through configuration (environment variable
    /// <c>Jwt__Key</c> in deployment) and never committed to the repository.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 480;

    /// <summary>Minimum key length for HMAC-SHA256, in bytes.</summary>
    public const int MinimumKeyBytes = 32;
}
