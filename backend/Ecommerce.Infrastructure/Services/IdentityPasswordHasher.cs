using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Microsoft.AspNetCore.Identity;

namespace Ecommerce.Infrastructure.Services;

/// <summary>
/// Password hashing via ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/>
/// (PBKDF2 with a per-password random salt, versioned format).
/// </summary>
/// <remarks>
/// Replaces the legacy Identity 2.2.2 configuration, whose policy allowed any 6 characters with no
/// complexity requirement (audit finding F-29). Complexity is now enforced separately by
/// <c>PasswordPolicy</c> before this type is ever called.
/// </remarks>
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<Customer> _hasher = new();

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return _hasher.HashPassword(new Customer(), password);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        try
        {
            return _hasher.VerifyHashedPassword(new Customer(), hash, password)
                   != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // Malformed stored hash: treat as a failed verification rather than a 500.
            return false;
        }
    }
}