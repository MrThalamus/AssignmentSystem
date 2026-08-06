using System.Text;
using System.Text.Json.Serialization;
using AssignmentSystem.Api.Middleware;
using AssignmentSystem.Api.Security;
using AssignmentSystem.Application;
using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Infrastructure;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Security;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configuration is layered appsettings -> environment variables, so a deployment can
// override any value (notably Jwt__Key and the connection string) without a rebuild.
builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as their names ("Published") rather than ordinals, which keeps
        // the API readable and lets the enum be extended without breaking clients.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// -------------------------------------------------------------------- auth

var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                  ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwtSettings.Key)
    || Encoding.UTF8.GetByteCount(jwtSettings.Key) < JwtSettings.MinimumKeyBytes)
{
    // Failing at startup is better than issuing tokens signed with a weak or absent
    // key and discovering it in production.
    throw new InvalidOperationException(
        $"Jwt:Key must be configured with at least {JwtSettings.MinimumKeyBytes} bytes. "
        + "Set the Jwt__Key environment variable (see .env.example).");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Claims are read exactly as issued; the default inbound mapping would rewrite
        // "sub" and "role" into long WS-Federation URIs.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            RoleClaimType = JwtTokenGenerator.RoleClaim,
            NameClaimType = JwtTokenGenerator.NameClaim,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

// -------------------------------------------------------------------- cors

const string FrontendCorsPolicy = "FrontendCorsPolicy";

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:3000"];

builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

// ----------------------------------------------------------------- swagger

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Assignment & Submission Management API",
        Version = "v1",
        Description = "Role-based API for creating assignments, submitting answers, and grading them."
    });

    const string bearerSchemeId = "Bearer";

    options.AddSecurityDefinition(bearerSchemeId, new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by POST /api/auth/login."
    });

    // OpenAPI.NET v2 wants a reference object here rather than the scheme itself, so
    // the requirement points at the definition registered above by id.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference(bearerSchemeId, document)] = []
    });

    var xmlPath = Path.Combine(AppContext.BaseDirectory, "AssignmentSystem.Api.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

var app = builder.Build();

// ----------------------------------------------------------------- pipeline

await ApplyDatabaseStartupTasksAsync(app);

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Assignment System API v1");
    options.DocumentTitle = "Assignment System API";
});

app.UseCors(FrontendCorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Unauthenticated liveness probe, handy for container health checks.
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

// Readiness probe that actually opens a connection and runs a query.
//
// Deliberately separate from /health rather than folded into it. Render's health
// check points at /health, so making that one touch the database would let a brief
// database outage restart an API that is otherwise perfectly healthy. This endpoint
// also gives an uptime pinger something to call that keeps the serverless database
// awake - hitting /health only keeps the container awake, and the database suspends
// on its own idle timer regardless.
app.MapGet("/health/db", async (ApplicationDbContext db, ILogger<Program> logger, CancellationToken ct) =>
{
    try
    {
        await db.Database.ExecuteSqlRawAsync("SELECT 1", ct);
        return Results.Ok(new { status = "healthy", database = "reachable" });
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "Readiness probe could not reach the database");

        return Results.Problem(
            title: "The database is not reachable.",
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).AllowAnonymous();

app.Run();

// Brings the database up to date before the first request. Migrating on startup is
// what lets an evaluator run the project without creating tables by hand; both steps
// are switchable so a real deployment can migrate as a separate, deliberate step.
static async Task ApplyDatabaseStartupTasksAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Database:AutoMigrate", true))
        return;

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");

        if (app.Configuration.GetValue($"{SeedOptions.SectionName}:Enabled", true))
        {
            var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
            await seeder.SeedAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex,
            "Database initialisation failed. Check the connection string and that PostgreSQL is running.");
        throw;
    }
}

/// <summary>Exposed so the test project can reference types from the API assembly.</summary>
public partial class Program;
