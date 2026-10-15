using Ecommerce.Core.Domain;
using Ecommerce.Core.Dtos.Auth;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ecommerce.Infrastructure.Services;

/// <summary>
/// Registration, login, refresh-token rotation and logout.
/// </summary>
/// <remarks>
/// Security properties:
/// <list type="bullet">
/// <item>Passwords are hashed with ASP.NET Core Identity's <c>PasswordHasher</c>; plaintext is never persisted.</item>
/// <item>Refresh tokens are random 48-byte values stored only as a SHA-256 digest.</item>
/// <item>Refresh rotates: the presented token is revoked and replaced. Presenting an already-rotated
/// token revokes the whole family (reuse detection).</item>
/// <item>Registration always assigns <see cref="Roles.Customer"/>. A client can never self-assign Admin.</item>
/// </list>
/// </remarks>
public sealed class AuthService(
    EcommerceDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var exists = await db.Customers.AnyAsync(c => c.Email == email, ct);
        if (exists)
        {
            throw new DuplicateEmailException(email);
        }

        var customer = new Customer
        {
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            // Role is fixed server-side. Never taken from the request body.
            Role = Roles.Customer,
            PasswordHash = passwordHasher.Hash(request.Password),
            CreatedAt = DateTime.UtcNow,
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return await IssueAsync(customer, ct);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Email == email, ct);
        if (customer is null || !passwordHasher.Verify(request.Password, customer.PasswordHash))
        {
            // Single generic message: never reveal whether the account exists.
            throw new InvalidCredentialsException();
        }

        return await IssueAsync(customer, ct);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidRefreshTokenException();
        }

        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens
            .Include(t => t.Customer)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null)
        {
            throw new InvalidRefreshTokenException();
        }

        // Reuse detection: a token that was already rotated is being replayed. Revoke the family.
        if (stored.RevokedAt is not null)
        {
            await db.RefreshTokens
                .Where(t => t.CustomerId == stored.CustomerId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
            throw new InvalidRefreshTokenException("Refresh token has already been used.");
        }

        if (stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw new InvalidRefreshTokenException("Refresh token has expired.");
        }

        var customer = stored.Customer
            ?? await db.Customers.FirstOrDefaultAsync(c => c.Id == stored.CustomerId, ct);

        if (customer is null)
        {
            // The token outlived its account.
            throw new InvalidRefreshTokenException("The account for this token no longer exists.");
        }

        return await IssueAsync(customer, ct, revokeHash: hash);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = JwtTokenService.HashRefreshToken(refreshToken);
        await db.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
    }

    public async Task<UserResponse?> GetUserAsync(int customerId, CancellationToken ct = default)
    {
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        return customer is null ? null : ToUserResponse(customer);
    }

    private async Task<AuthResult> IssueAsync(
        Customer customer,
        CancellationToken ct,
        string? revokeHash = null)
    {
        if (revokeHash is not null)
        {
            var previous = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == revokeHash, ct);
            if (previous is not null)
            {
                previous.RevokedAt = DateTime.UtcNow;
            }
        }

        var (accessToken, expiresIn) = tokenService.CreateAccessToken(customer);

        var rawRefresh = JwtTokenService.CreateRawRefreshToken();
        var newHash = JwtTokenService.HashRefreshToken(rawRefresh);

        db.RefreshTokens.Add(new RefreshToken
        {
            CustomerId = customer.Id,
            TokenHash = newHash,
            ExpiresAt = DateTime.UtcNow.AddDays(Math.Max(1, _jwt.RefreshTokenDays)),
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);

        return new AuthResult(
            new AuthResponse(accessToken, "Bearer", expiresIn, ToUserResponse(customer)),
            rawRefresh,
            DateTime.UtcNow.AddDays(Math.Max(1, _jwt.RefreshTokenDays)));
    }

    private static UserResponse ToUserResponse(Customer c) => new(
        c.Id, c.Email, c.FirstName, c.LastName, c.Role, c.FullName);
}

/// <summary>Registration/login failed because the email is already taken.</summary>
public sealed class DuplicateEmailException(string email)
    : Exception($"An account with the email '{email}' already exists.");

/// <summary>Login failed. Deliberately does not distinguish unknown-user from bad-password.</summary>
public sealed class InvalidCredentialsException()
    : Exception("Invalid email or password.");

/// <summary>The supplied refresh token is unknown, expired, or was already used.</summary>
public sealed class InvalidRefreshTokenException(string? message = "Invalid refresh token.")
    : Exception(message);