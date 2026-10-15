using Ecommerce.Core.Dtos.Account;
using Ecommerce.Core.Interfaces.Services;
using Ecommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Ecommerce.Infrastructure.Services;

/// <summary>
/// Profile and address-book operations. Every query is scoped by <c>customerId</c> taken from the
/// validated access token, so a customer can never read or mutate another customer's data.
/// </summary>
public sealed class AccountService(EcommerceDbContext db) : IAccountService
{
    public async Task<ProfileDto?> GetProfileAsync(int customerId, CancellationToken ct = default)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == customerId, ct);
        return c is null ? null : ToProfile(c);
    }

    public async Task<ProfileDto?> UpdateProfileAsync(int customerId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var c = await db.Customers.FirstOrDefaultAsync(x => x.Id == customerId, ct);
        if (c is null)
        {
            return null;
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (!string.Equals(email, c.Email, StringComparison.Ordinal))
        {
            var taken = await db.Customers.AnyAsync(x => x.Email == email && x.Id != customerId, ct);
            if (taken)
            {
                throw new DuplicateEmailException(email);
            }
        }

        c.Email = email;
        c.FirstName = request.FirstName.Trim();
        c.LastName = request.LastName.Trim();

        await db.SaveChangesAsync(ct);
        return ToProfile(c);
    }

    public async Task<IReadOnlyList<AddressDto>> GetAddressesAsync(int customerId, CancellationToken ct = default) =>
        await db.Addresses.AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.Id)
            .Select(a => new AddressDto(
                a.Id, a.FirstName, a.LastName, a.FirstName + " " + a.LastName,
                a.Line1, a.Line2, a.City, a.State, a.PostalCode, a.Country, a.Phone, a.IsDefault))
            .ToListAsync(ct);

    public async Task<AddressDto> AddAddressAsync(int customerId, CreateAddressRequest request, CancellationToken ct = default)
    {
        var isFirst = !await db.Addresses.AnyAsync(a => a.CustomerId == customerId, ct);
        var makeDefault = request.IsDefault || isFirst;

        if (makeDefault)
        {
            await ClearDefaultsAsync(customerId, ct);
        }

        var address = new Core.Entities.Address
        {
            CustomerId = customerId,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Line1 = request.Line1.Trim(),
            Line2 = request.Line2?.Trim(),
            City = request.City.Trim(),
            State = request.State.Trim(),
            PostalCode = request.PostalCode.Trim(),
            Country = request.Country.Trim(),
            Phone = request.Phone?.Trim(),
            IsDefault = makeDefault,
        };

        db.Addresses.Add(address);
        await db.SaveChangesAsync(ct);

        return ToDto(address);
    }

    public async Task<AddressDto?> UpdateAddressAsync(int customerId, int addressId, UpdateAddressRequest request, CancellationToken ct = default)
    {
        var address = await db.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, ct);

        if (address is null)
        {
            return null;
        }

        if (request.IsDefault && !address.IsDefault)
        {
            await ClearDefaultsAsync(customerId, ct);
        }

        address.FirstName = request.FirstName.Trim();
        address.LastName = request.LastName.Trim();
        address.Line1 = request.Line1.Trim();
        address.Line2 = request.Line2?.Trim();
        address.City = request.City.Trim();
        address.State = request.State.Trim();
        address.PostalCode = request.PostalCode.Trim();
        address.Country = request.Country.Trim();
        address.Phone = request.Phone?.Trim();
        // An address can be promoted to default but never silently demoted by an edit.
        if (request.IsDefault)
        {
            address.IsDefault = true;
        }

        await db.SaveChangesAsync(ct);
        return ToDto(address);
    }

    public async Task<bool> DeleteAddressAsync(int customerId, int addressId, CancellationToken ct = default)
    {
        var address = await db.Addresses
            .FirstOrDefaultAsync(a => a.Id == addressId && a.CustomerId == customerId, ct);

        if (address is null)
        {
            return false;
        }

        var wasDefault = address.IsDefault;

        // Orders hold their own address snapshots, so the reference can be cleared before the
        // row goes. This also avoids a cascade-path cycle on SQL Server.
        await db.Orders
            .Where(o => o.ShippingAddressId == addressId || o.BillingAddressId == addressId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(o => o.ShippingAddressId, (int?)null)
                    .SetProperty(o => o.BillingAddressId, (int?)null),
                ct);

        db.Addresses.Remove(address);
        await db.SaveChangesAsync(ct);

        if (wasDefault)
        {
            // Promote another address so the customer is never left without a default.
            var next = await db.Addresses
                .Where(a => a.CustomerId == customerId)
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(ct);

            if (next is not null)
            {
                next.IsDefault = true;
                await db.SaveChangesAsync(ct);
            }
        }

        return true;
    }

    private async Task ClearDefaultsAsync(int customerId, CancellationToken ct) =>
        await db.Addresses
            .Where(a => a.CustomerId == customerId && a.IsDefault)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsDefault, false), ct);

    private static AddressDto ToDto(Core.Entities.Address a) => new(
        a.Id, a.FirstName, a.LastName, a.FullName, a.Line1, a.Line2, a.City, a.State,
        a.PostalCode, a.Country, a.Phone, a.IsDefault);

    private static ProfileDto ToProfile(Core.Entities.Customer c) => new(
        c.Id, c.Email, c.FirstName, c.LastName, c.FullName, c.Role, c.CreatedAt);
}