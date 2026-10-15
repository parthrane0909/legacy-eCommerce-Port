namespace Ecommerce.Core.Dtos.Account;

public record ProfileDto(
    int Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    string Role,
    DateTime CreatedAt);

public record UpdateProfileRequest(string FirstName, string LastName, string Email);

public record AddressDto(
    int Id,
    string FirstName,
    string LastName,
    string FullName,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone,
    bool IsDefault);

public record CreateAddressRequest(
    string FirstName,
    string LastName,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone,
    bool IsDefault = false);

public record UpdateAddressRequest(
    string FirstName,
    string LastName,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Phone,
    bool IsDefault = false);