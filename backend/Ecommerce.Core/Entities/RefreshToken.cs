namespace Ecommerce.Core.Entities;

/// <summary>
/// A persisted, single-use refresh token. Tokens are stored only as a SHA-256 hash so a database
/// leak does not yield usable credentials. Rotation revokes the consumed token and issues a new one.
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>SHA-256 hex digest of the opaque token value. The raw value is never persisted.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    /// <summary>Set when this token is replaced by a rotation, to detect token reuse.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive(DateTime utcNow) => RevokedAt is null && ExpiresAt > utcNow;
}