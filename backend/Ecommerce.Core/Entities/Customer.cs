namespace Ecommerce.Core.Entities;

/// <summary>Application customer and the single role they hold.</summary>
public class Customer
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    /// <summary>Never a plaintext password. Produced by <c>IPasswordHasher</c>.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>Either <see cref="Roles.Customer"/> or <see cref="Roles.Admin"/>.</summary>
    public string Role { get; set; } = Roles.Customer;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Address> Addresses { get; set; } = new List<Address>();

    public ICollection<Cart> Carts { get; set; } = new List<Cart>();

    public ICollection<Order> Orders { get; set; } = new List<Order>();

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public string FullName => $"{FirstName} {LastName}".Trim();
}