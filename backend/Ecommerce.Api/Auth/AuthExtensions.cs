using System.Security.Claims;
using Ecommerce.Core.Entities;
using Ecommerce.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Api.Auth;

/// <summary>
/// Configures JWT bearer authentication and the authorization policies used by the endpoints.
/// </summary>
public static class AuthExtensions
{
    /// <summary>Policy name for administrator-only endpoints.</summary>
    public const string AdminPolicy = "AdminOnly";

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("JWT configuration is missing.");

        Validate(jwt);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimTypes.Name,
                    RoleClaimType = ClaimTypes.Role,
                };

                // Keep the original claim types so "sub" stays "sub" instead of being
                // rewritten to the long WS-Federation URI form.
                options.MapInboundClaims = false;
            });

        // Bind the issuer, audience and signing key from IOptions<JwtOptions> rather than from the
        // configuration snapshot taken while this method runs. Configuration can be layered in after
        // this point (a test host does exactly that), and capturing early produced a bearer handler
        // with no signing key - surfacing as "The signature key was not found".
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, options) =>
            {
                var value = options.Value;
                bearer.TokenValidationParameters.ValidIssuer = value.Issuer;
                bearer.TokenValidationParameters.ValidAudience = value.Audience;
                bearer.TokenValidationParameters.IssuerSigningKey = SigningKey(value);
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy =>
                policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        return services;
    }

    private static SymmetricSecurityKey SigningKey(JwtOptions jwt) =>
        new(System.Text.Encoding.UTF8.GetBytes(jwt.SigningKey));

    private static void Validate(JwtOptions jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 characters. Set it via appsettings, "
                + "user-secrets or the SHOPHUB_JWT__SIGNINGKEY environment variable.");
        }
    }
}