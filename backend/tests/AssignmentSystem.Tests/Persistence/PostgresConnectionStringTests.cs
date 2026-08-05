using AssignmentSystem.Infrastructure.Persistence;
using Npgsql;

namespace AssignmentSystem.Tests.Persistence;

/// <summary>
/// Hosted PostgreSQL providers issue a URI that Npgsql cannot parse. Getting this
/// conversion wrong shows up only at deployment time, as a connection failure whose
/// message points nowhere useful, so it is pinned here.
/// </summary>
public class PostgresConnectionStringTests
{
    private static NpgsqlConnectionStringBuilder Parse(string value) =>
        new(PostgresConnectionString.Normalise(value));

    [Fact]
    public void A_keyword_connection_string_is_left_alone()
    {
        const string keyword =
            "Host=localhost;Port=5432;Database=assignment_system;Username=postgres;Password=postgres";

        Assert.Equal(keyword, PostgresConnectionString.Normalise(keyword));
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        // Pasting into a dashboard field very often brings a trailing newline.
        var result = PostgresConnectionString.Normalise("  Host=localhost;Database=db  ");

        Assert.Equal("Host=localhost;Database=db", result);
    }

    [Fact]
    public void A_provider_uri_is_converted_to_keyword_form()
    {
        var builder = Parse(
            "postgresql://neon_user:secret123@ep-cool-name-123.us-east-2.aws.neon.tech/neondb?sslmode=require");

        Assert.Equal("ep-cool-name-123.us-east-2.aws.neon.tech", builder.Host);
        Assert.Equal("neondb", builder.Database);
        Assert.Equal("neon_user", builder.Username);
        Assert.Equal("secret123", builder.Password);
        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void The_shorter_postgres_scheme_is_also_accepted()
    {
        var builder = Parse("postgres://user:pass@db.example.com/appdb");

        Assert.Equal("db.example.com", builder.Host);
        Assert.Equal("appdb", builder.Database);
    }

    [Fact]
    public void A_uri_without_a_port_falls_back_to_the_postgres_default()
    {
        // Uri reports -1 rather than 5432 when no port is given.
        Assert.Equal(5432, Parse("postgresql://user:pass@host/db").Port);
    }

    [Fact]
    public void An_explicit_port_is_preserved()
    {
        Assert.Equal(6543, Parse("postgresql://user:pass@host:6543/db").Port);
    }

    [Fact]
    public void Percent_encoded_credentials_are_decoded()
    {
        // A generated password containing '@' or '/' arrives escaped; handing the
        // escaped text to Npgsql would authenticate with the wrong password.
        var builder = Parse("postgresql://my%40user:p%40ss%2Fword%21@host/db");

        Assert.Equal("my@user", builder.Username);
        Assert.Equal("p@ss/word!", builder.Password);
    }

    [Fact]
    public void TLS_is_required_even_when_the_uri_does_not_say_so()
    {
        // A managed database should never be reached over plaintext by default.
        Assert.Equal(SslMode.Require, Parse("postgresql://user:pass@host/db").SslMode);
    }

    [Theory]
    [InlineData("disable", SslMode.Disable)]
    [InlineData("prefer", SslMode.Prefer)]
    [InlineData("require", SslMode.Require)]
    [InlineData("verify-ca", SslMode.VerifyCA)]
    [InlineData("verify-full", SslMode.VerifyFull)]
    public void The_sslmode_parameter_is_honoured(string value, SslMode expected)
    {
        Assert.Equal(expected, Parse($"postgresql://user:pass@host/db?sslmode={value}").SslMode);
    }

    [Fact]
    public void Unsupported_query_parameters_are_ignored_rather_than_rejected()
    {
        // Neon appends channel_binding, which has no Npgsql equivalent. Passing it
        // through would make the builder throw on an otherwise valid string.
        var builder = Parse(
            "postgresql://user:pass@host/db?sslmode=require&channel_binding=require&application_name=demo");

        Assert.Equal("host", builder.Host);
        Assert.Equal(SslMode.Require, builder.SslMode);
    }

    [Fact]
    public void An_empty_connection_string_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => PostgresConnectionString.Normalise("   "));
    }

    [Fact]
    public void A_malformed_uri_reports_what_was_expected()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => PostgresConnectionString.Normalise("postgresql://"));

        Assert.Contains("postgresql://user:password@host", error.Message);
    }
}
