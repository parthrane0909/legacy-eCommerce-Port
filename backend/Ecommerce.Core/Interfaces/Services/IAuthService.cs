using Ecommerce.Core.Dtos.Auth;

namespace Ecommerce.Core.Interfaces.Services;

/// <summary>Result of a successful authentication: the API response plus the raw refresh token.</summary>
public record AuthResult(AuthResponse Response, string RefreshToken, DateTime RefreshTokenExpiresAt);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>Rotates the supplied refresh token, revoking it and issuing a replacement.</summary>
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Revokes the supplied refresh token. Idempotent.</summary>
    Task LogoutAsync(string? refreshToken, CancellationToken ct = default);

    Task<UserResponse?> GetUserAsync(int customerId, CancellationToken ct = default);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}