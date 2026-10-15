using Ecommerce.Core.Entities;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Services;

namespace Ecommerce.Tests.Unit;

/// <summary>
/// Unit tests for password hashing. Verifies the hashes are salted, verifiable, and that the
/// legacy weakness (any 6 characters) is no longer sufficient on its own.
/// </summary>
public class PasswordHasherTests
{
    private readonly IPasswordHasher _hasher = new IdentityPasswordHasher();

    [Fact]
    public void Hashed_password_verifies()
    {
        var hash = _hasher.Hash("Str0ng!Password");

        _hasher.Verify("Str0ng!Password", hash).Should().BeTrue();
    }

    [Fact]
    public void Wrong_password_does_not_verify()
    {
        var hash = _hasher.Hash("Str0ng!Password");

        _hasher.Verify("Wrong!Password", hash).Should().BeFalse();
    }

    [Fact]
    public void Hashes_are_salted_so_equal_passwords_differ()
    {
        var a = _hasher.Hash("Str0ng!Password");
        var b = _hasher.Hash("Str0ng!Password");

        a.Should().NotBe(b);
        _hasher.Verify("Str0ng!Password", a).Should().BeTrue();
        _hasher.Verify("Str0ng!Password", b).Should().BeTrue();
    }

    [Fact]
    public void Plaintext_is_never_embedded_in_the_hash()
    {
        const string password = "Str0ng!Password";

        _hasher.Hash(password).Should().NotContain(password);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Blank_input_is_rejected_or_fails_closed(string? input)
    {
        FluentActions.Invoking(() => _hasher.Hash(input!)).Should().Throw<ArgumentException>();

        _hasher.Verify(input!, "somehash").Should().BeFalse();
        _hasher.Verify("Str0ng!Password", input!).Should().BeFalse();
    }

    [Fact]
    public void Malformed_hash_fails_closed_instead_of_throwing()
    {
        _hasher.Verify("Str0ng!Password", "not-a-valid-hash").Should().BeFalse();
    }
}

public class RefreshTokenHashingTests
{
    [Fact]
    public void Raw_tokens_are_random_and_long()
    {
        var tokens = Enumerable.Range(0, 500)
            .Select(_ => JwtTokenService.CreateRawRefreshToken())
            .ToList();

        tokens.Distinct().Should().HaveCount(500);
        tokens.Should().OnlyContain(t => t.Length >= 60);
    }

    [Fact]
    public void Hashing_is_deterministic_so_lookup_works()
    {
        const string raw = "some-raw-token";

        var first = JwtTokenService.HashRefreshToken(raw);
        var second = JwtTokenService.HashRefreshToken(raw);

        first.Should().Be(second);
        first.Should().NotBe(raw);
        first.Should().HaveLength(64, "SHA-256 renders as 64 hex characters");
    }

    [Fact]
    public void Different_tokens_hash_differently() =>
        JwtTokenService.HashRefreshToken("a")
            .Should().NotBe(JwtTokenService.HashRefreshToken("b"));
}

public class EntityComputationTests
{
    [Fact]
    public void CartItem_LineTotal_is_quantity_times_price()
    {
        var item = new CartItem { UnitPrice = 149.99m, Quantity = 3 };

        item.LineTotal.Should().Be(449.97m);
    }

    [Fact]
    public void OrderItem_LineTotal_rounds_to_two_places()
    {
        var item = new OrderItem { UnitPrice = 33.333m, Quantity = 3 };

        item.LineTotal.Should().Be(100.00m);
    }

    [Fact]
    public void Customer_FullName_joins_the_name_parts()
    {
        new Customer { FirstName = "Aditi", LastName = "Rao" }.FullName.Should().Be("Aditi Rao");
        new Customer { FirstName = "Aditi" }.FullName.Should().Be("Aditi");
    }

    [Fact]
    public void Address_FullName_joins_the_name_parts() =>
        new Address { FirstName = "Rohan", LastName = "Mehta" }.FullName.Should().Be("Rohan Mehta");

    [Fact]
    public void Cart_reports_guest_status_from_its_owner()
    {
        new Cart { CustomerId = 7 }.IsGuest.Should().BeFalse();
        new Cart { AnonymousId = "abc" }.IsGuest.Should().BeTrue();
    }

    [Fact]
    public void RefreshToken_activity_depends_on_revocation_and_expiry()
    {
        var now = DateTime.UtcNow;

        new RefreshToken { ExpiresAt = now.AddMinutes(5) }.IsActive(now).Should().BeTrue();
        new RefreshToken { ExpiresAt = now.AddMinutes(-1) }.IsActive(now).Should().BeFalse();
        new RefreshToken { ExpiresAt = now.AddMinutes(5), RevokedAt = now }.IsActive(now).Should().BeFalse();
    }

    [Fact]
    public void Shipping_options_expose_the_legacy_price_table()
    {
        ShippingOptions.All.Should().HaveCount(2);
        ShippingOptions.CostFor("Express").Should().Be(350m);
        ShippingOptions.CostFor("Standard").Should().Be(150m);
        ShippingOptions.CostFor(null).Should().Be(150m);
    }
}