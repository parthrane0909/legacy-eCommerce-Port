namespace Ecommerce.Core.Dtos.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string FirstName,
    string LastName);

public record LoginRequest(
    string Email,
    string Password);

/// <summary>Returned by register/login/refresh. The refresh token travels in an HttpOnly cookie.</summary>
public record AuthResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInSeconds,
    UserResponse User);

public record UserResponse(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    string FullName);

/// <summary>
/// Body for POST /api/auth/refresh. The refresh token normally arrives via the HttpOnly cookie;
/// this DTO exists so an explicit body token is also accepted (and so Swagger documents it).
/// </summary>
public record RefreshRequest(string? RefreshToken);