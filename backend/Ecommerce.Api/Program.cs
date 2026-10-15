using System.Text;
using System.Text.Json.Serialization;
using Ecommerce.Api.Auth;
using Ecommerce.Api.Endpoints;
using Ecommerce.Api.Infrastructure;
using Ecommerce.Infrastructure;
using Ecommerce.Infrastructure.Services;
using FluentValidation;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Events;

const string CorsPolicyName = "ShophubFrontend";

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Serilog: structured logging for requests and errors.
// ---------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Per-request: CurrentUser depends on the scoped ITokenService and IHttpContextAccessor.
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ConcurrencyGate>();
builder.Services.AddHttpContextAccessor();

// ---------------------------------------------------------------------------
// Infrastructure: DbContext + application services.
// ---------------------------------------------------------------------------
builder.Services.AddEcommerceInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Auth, CORS, rate limiting.
// ---------------------------------------------------------------------------
builder.Services.AddJwtAuthentication(builder.Configuration);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // No trusted origins configured: refuse all cross-origin calls rather than allow any.
            policy.WithOrigins().AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials(); // Required for the HttpOnly refresh-token cookie.
        }
    });
});

// Rate limiting can be switched off for load testing and integration tests via configuration
// (RateLimiting:Enabled). The setting is evaluated per request rather than captured at start-up
// so that configuration added by a test host is honoured.
var rateLimitConfig = builder.Configuration;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Per-IP limit on the credential endpoints to blunt brute-force attempts.
    options.AddPolicy(AuthEndpoints.AuthRateLimitPolicy, httpContext =>
        rateLimitConfig.GetValue("RateLimiting:Enabled", true)
            ? RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitConfig.GetValue("RateLimiting:AuthPermitLimit", 10),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                })
            : RateLimitPartition.GetNoLimiter("disabled"));

    // A broader safety net for everything else.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        rateLimitConfig.GetValue("RateLimiting:Enabled", true)
            ? RateLimitPartition.GetFixedWindowLimiter(
                PartitionKey(httpContext),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rateLimitConfig.GetValue("RateLimiting:GlobalPermitLimit", 300),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                })
            : RateLimitPartition.GetNoLimiter("disabled"));

    static string PartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
});

// ---------------------------------------------------------------------------
// Validation, ProblemDetails, OpenAPI, health.
// ---------------------------------------------------------------------------
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = ctx =>
    {
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
        ctx.ProblemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// One OpenAPI document, produced by Microsoft.AspNetCore.OpenApi and served at
// /openapi/v1.json (the path the assignment specifies). Swagger UI renders that same
// document, so there is no second generator that could drift out of sync.
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info ??= new OpenApiInfo();
        document.Info.Title = "ShopHub API";
        document.Info.Version = "v1";
        document.Info.Description =
            "REST API for the modernized ShopHub storefront. "
            + "Access tokens are short-lived JWTs kept in memory by the client; "
            + "refresh tokens are delivered as an HttpOnly cookie.";

        // Declare the bearer scheme so Swagger UI offers an "Authorize" button.
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste the access token returned by /api/auth/login.",
        };

        // Document-level security requirement (OpenAPI.NET 2.x exposes this as `Security`).
        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });

        return Task.CompletedTask;
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<EcommerceDbContext>(name: "database", tags: ["ready"]);

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline.
// ---------------------------------------------------------------------------
app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    // Swagger UI renders the SAME document that /openapi/v1.json serves. The previous
    // configuration pointed SwaggerEndpoint at "/swagger/v1/swagger-ui.html" — the UI's own
    // HTML page, not a specification — which returned 404, so the UI loaded with no operations.
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "ShopHub API v1"));
}

app.UseCors(CorsPolicyName);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
        };
        await context.Response.WriteAsJsonAsync(payload);
    },
});

app.MapOpenApi(); // Serves the document at /openapi/v1.json

// A guard against a whole class of bug: services are registered per-request against one
// DbContext, so no request handler may start two database operations concurrently.
app.Use(async (context, next) =>
{
    var services = context.RequestServices;
    var gate = services.GetRequiredService<ConcurrencyGate>();
    await gate.WaitAsync(context.RequestAborted);
    try
    {
        await next();
    }
    finally
    {
        gate.Release();
    }
});

app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapCartEndpoints();
app.MapOrderEndpoints();
app.MapAccountEndpoints();
app.MapAdminEndpoints();

// Apply migrations and seed on start-up.
if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    try
    {
        await app.Services.InitialiseDatabaseAsync();
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database initialisation failed.");
        throw;
    }
}

Log.Information("ShopHub API starting in {Environment} mode.", app.Environment);

await app.RunAsync();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can bootstrap the test host.</summary>
public partial class Program;