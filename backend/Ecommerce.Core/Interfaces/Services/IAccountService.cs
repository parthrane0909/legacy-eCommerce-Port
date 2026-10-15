using Ecommerce.Core.Dtos.Account;

namespace Ecommerce.Core.Interfaces.Services;

public interface IAccountService
{
    Task<ProfileDto?> GetProfileAsync(int customerId, CancellationToken ct = default);

    Task<ProfileDto?> UpdateProfileAsync(int customerId, UpdateProfileRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<AddressDto>> GetAddressesAsync(int customerId, CancellationToken ct = default);

    /// <summary>Adds an address. Returns non-null: a successful save always yields a readable row.</summary>
    Task<AddressDto> AddAddressAsync(int customerId, CreateAddressRequest request, CancellationToken ct = default);

    /// <summary>Returns null when the address does not exist or belongs to another customer.</summary>
    Task<AddressDto?> UpdateAddressAsync(int customerId, int addressId, UpdateAddressRequest request, CancellationToken ct = default);

    /// <summary>Returns false when the address does not exist or belongs to another customer.</summary>
    Task<bool> DeleteAddressAsync(int customerId, int addressId, CancellationToken ct = default);
}