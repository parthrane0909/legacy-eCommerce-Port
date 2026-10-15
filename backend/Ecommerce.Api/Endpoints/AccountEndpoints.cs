using Ecommerce.Api.Auth;
using Ecommerce.Api.Validation;
using Ecommerce.Core.Dtos.Account;
using Ecommerce.Core.Interfaces.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Ecommerce.Api.Endpoints;

/// <summary>
/// /api/account/* — profile and address book. Every query is scoped to the authenticated customer,
/// so one customer can never read or mutate another customer's data.
/// </summary>
public static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account")
            .WithTags("Account")
            .RequireAuthorization();

        group.MapGet("/profile", GetProfileAsync)
            .WithName("GetProfile")
            .WithSummary("Current customer's profile")
            .Produces<ProfileDto>();

        group.MapPut("/profile", UpdateProfileAsync)
            .WithValidatedRequest<UpdateProfileRequest>()
            .WithName("UpdateProfile")
            .WithSummary("Update first name, last name and email")
            .Produces<ProfileDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/addresses", GetAddressesAsync)
            .WithName("ListAddresses")
            .WithSummary("Customer's saved addresses")
            .Produces<IReadOnlyList<AddressDto>>();

        group.MapPost("/addresses", AddAddressAsync)
            .WithValidatedRequest<CreateAddressRequest>()
            .WithName("CreateAddress")
            .WithSummary("Add an address")
            .Produces<AddressDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/addresses/{id:int:min(1)}", UpdateAddressAsync)
            .WithValidatedRequest<UpdateAddressRequest>()
            .WithName("UpdateAddress")
            .WithSummary("Update one of the customer's own addresses")
            .Produces<AddressDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/addresses/{id:int:min(1)}", DeleteAddressAsync)
            .WithName("DeleteAddress")
            .WithSummary("Delete one of the customer's own addresses")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<ProfileDto>, NotFound, UnauthorizedHttpResult>> GetProfileAsync(
        ICurrentUser current, IAccountService account, CancellationToken ct)
    {
        if (current.CustomerId is not { } customerId)
        {
            return TypedResults.Unauthorized();
        }

        var profile = await account.GetProfileAsync(customerId, ct);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
    }

    private static async Task<Results<Ok<ProfileDto>, NotFound, UnauthorizedHttpResult>> UpdateProfileAsync(
        UpdateProfileRequest request, ICurrentUser current, IAccountService account, CancellationToken ct)
    {
        if (current.CustomerId is not { } customerId)
        {
            return TypedResults.Unauthorized();
        }

        var profile = await account.UpdateProfileAsync(customerId, request, ct);
        return profile is null ? TypedResults.NotFound() : TypedResults.Ok(profile);
    }

    private static Task<IReadOnlyList<AddressDto>> GetAddressesAsync(
        ICurrentUser current, IAccountService account, CancellationToken ct) =>
        account.GetAddressesAsync(current.CustomerId ?? 0, ct);

    private static async Task<Results<Created<AddressDto>, UnauthorizedHttpResult>> AddAddressAsync(
        CreateAddressRequest request, ICurrentUser current, IAccountService account, CancellationToken ct)
    {
        if (current.CustomerId is not { } customerId)
        {
            return TypedResults.Unauthorized();
        }

        var address = await account.AddAddressAsync(customerId, request, ct);
        return TypedResults.Created($"/api/account/addresses/{address.Id}", address);
    }

    private static async Task<Results<Ok<AddressDto>, NotFound, UnauthorizedHttpResult>> UpdateAddressAsync(
        int id, UpdateAddressRequest request, ICurrentUser current, IAccountService account, CancellationToken ct)
    {
        if (current.CustomerId is not { } customerId)
        {
            return TypedResults.Unauthorized();
        }

        var address = await account.UpdateAddressAsync(customerId, id, request, ct);
        return address is null ? TypedResults.NotFound() : TypedResults.Ok(address);
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> DeleteAddressAsync(
        int id, ICurrentUser current, IAccountService account, CancellationToken ct)
    {
        if (current.CustomerId is not { } customerId)
        {
            return TypedResults.Unauthorized();
        }

        var deleted = await account.DeleteAddressAsync(customerId, id, ct);
        return deleted ? TypedResults.NoContent() : TypedResults.NotFound();
    }
}