using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ecommerce.Core.Common;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecommerce.Infrastructure.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}

/// <summary>Issues and validates JWT access tokens.</summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    public const string CustomerIdClaim = "sub";
    public const string EmailClaim = "email";
    public const string RoleClaim = "role";

    private readonly JwtOptions _options = options.Value;

    public int RefreshTokenDays => Math.Max(1, _options.RefreshTokenDays);

    public (string Token, int ExpiresInSeconds) CreateAccessToken(Customer customer)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(Math.Max(1, _options.AccessTokenMinutes));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, customer.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(ClaimTypes.NameIdentifier, customer.Id.ToString()),
            new(ClaimTypes.Name, customer.Email),
            new(ClaimTypes.Role, customer.Role),
        };

        // IdentityModel 8.x removed JwtSecurityToken/JwtSecurityTokenHandler in favour of
        // SecurityTokenDescriptor + JsonWebTokenHandler.
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            Expires = expires,
            IssuedAt = now,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        var handler = new JsonWebTokenHandler();
        return (handler.CreateToken(descriptor),
            (int)Math.Max(0, (expires - DateTime.UtcNow).TotalSeconds));
    }

    public int? GetCustomerId(ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(CustomerIdClaim)
                  ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(raw, out var id) ? id : null;
    }

    // ---- Refresh token helpers -------------------------------------------------

    public static string CreateRawRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public static string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}