using Ecommerce.Core.Dtos.Auth;
using Ecommerce.Core.Entities;

namespace Ecommerce.Core.Interfaces.Services;

public interface ITokenService
{
    /// <summary>Issues a short-lived signed JWT access token for the customer.</summary>
    (string Token, int ExpiresInSeconds) CreateAccessToken(Customer customer);

    /// <summary>Reads the customer id claim from a validated principal.</summary>
    int? GetCustomerId(ClaimsPrincipal principal);
}