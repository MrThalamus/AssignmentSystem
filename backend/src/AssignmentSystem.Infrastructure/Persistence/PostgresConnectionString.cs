using Npgsql;

namespace AssignmentSystem.Infrastructure.Persistence;

/// <summary>
/// Accepts either form of PostgreSQL connection string.
///
/// Hosted providers (Neon, Supabase, Railway, Heroku, ...) hand out a URI:
/// <c>postgresql://user:password@host/database?sslmode=require</c>. Npgsql only
/// understands the keyword form, and fed a URI it fails with a parse error that
/// says nothing about the real problem. Rather than making every deployment
/// rewrite the string by hand - fiddly, and easy to get subtly wrong - the
/// application converts it.
/// </summary>
public static class PostgresConnectionString
{
    private static readonly string[] UriSchemes = ["postgres", "postgresql"];

    private const string BadUriMessage =
        "The connection string looks like a URI but could not be parsed. Expected "
        + "postgresql://user:password@host:port/database.";

    /// <summary>
    /// Returns the string unchanged if it is already in keyword form, otherwise
    /// converts it from URI form.
    /// </summary>
    public static string Normalise(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("The connection string is empty.", nameof(connectionString));

        var value = connectionString.Trim();

        if (!LooksLikeUri(value))
            return value;

        return ConvertFromUri(value);
    }

    private static bool LooksLikeUri(string value) =>
        UriSchemes.Any(scheme => value.StartsWith($"{scheme}://", StringComparison.OrdinalIgnoreCase));

    private static string ConvertFromUri(string value)
    {
        Uri uri;

        try
        {
            uri = new Uri(value);
        }
        catch (UriFormatException ex)
        {
            throw new InvalidOperationException(BadUriMessage, ex);
        }

        // Uri accepts "postgresql://" and similar fragments without complaint, so the
        // parts that actually matter have to be checked rather than assumed.
        if (string.IsNullOrEmpty(uri.Host) || uri.AbsolutePath.Trim('/').Length == 0)
            throw new InvalidOperationException(BadUriMessage);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            // A URI without an explicit port reports -1 rather than the default.
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
        };

        // Credentials are percent-encoded in a URI, so a password containing '@' or
        // '/' arrives escaped and has to be decoded before Npgsql sees it.
        var credentials = uri.UserInfo.Split(':', 2);

        if (credentials.Length > 0 && credentials[0].Length > 0)
            builder.Username = Uri.UnescapeDataString(credentials[0]);

        if (credentials.Length > 1)
            builder.Password = Uri.UnescapeDataString(credentials[1]);

        ApplyQueryParameters(builder, uri.Query);

        return builder.ConnectionString;
    }

    private static void ApplyQueryParameters(NpgsqlConnectionStringBuilder builder, string query)
    {
        // Managed providers refuse unencrypted connections, and the URI they issue
        // says so via ?sslmode=require. Default to requiring TLS even when the
        // parameter is absent: for a hosted database that is the safe assumption.
        builder.SslMode = SslMode.Require;

        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var key = Uri.UnescapeDataString(parts[0]);
            var raw = Uri.UnescapeDataString(parts[1]);

            // Only sslmode is translated. Other libpq parameters (channel_binding,
            // application_name, options, ...) have no Npgsql equivalent or are not
            // needed to connect, and passing them through would break the builder.
            if (key.Equals("sslmode", StringComparison.OrdinalIgnoreCase)
                && TryParseSslMode(raw, out var sslMode))
            {
                builder.SslMode = sslMode;
            }
        }
    }

    private static bool TryParseSslMode(string value, out SslMode sslMode)
    {
        // libpq spells these differently from Npgsql's enum, so map the ones that
        // actually appear in provider-issued URIs.
        switch (value.Replace("-", string.Empty).ToLowerInvariant())
        {
            case "disable":
                sslMode = SslMode.Disable;
                return true;
            case "allow":
                sslMode = SslMode.Allow;
                return true;
            case "prefer":
                sslMode = SslMode.Prefer;
                return true;
            case "require":
                sslMode = SslMode.Require;
                return true;
            case "verifyca":
                sslMode = SslMode.VerifyCA;
                return true;
            case "verifyfull":
                sslMode = SslMode.VerifyFull;
                return true;
            default:
                sslMode = SslMode.Require;
                return false;
        }
    }
}
