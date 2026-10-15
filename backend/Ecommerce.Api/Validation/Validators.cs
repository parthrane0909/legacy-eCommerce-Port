using Ecommerce.Core.Domain;
using Ecommerce.Core.Entities;
using Ecommerce.Core.Dtos.Account;
using Ecommerce.Core.Dtos.Admin;
using Ecommerce.Core.Dtos.Auth;
using Ecommerce.Core.Dtos.Cart;
using Ecommerce.Core.Dtos.Orders;
using FluentValidation;

namespace Ecommerce.Api.Validation;

/// <summary>Validators for every request DTO. Failures surface as 400 ProblemDetails.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.")
            .MaximumLength(256);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .Must(p => PasswordPolicy.IsValid(p))
            .WithMessage("Password must be at least 8 characters and contain an uppercase letter, "
                         + "a lowercase letter, a digit and a non-alphanumeric character.");
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Enter a valid email address.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128);
    }
}

public sealed class AddCartItemRequestValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemRequestValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0).WithMessage("ProductId must be greater than 0.");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, CartMergePlanner.MaxQuantityPerLine)
            .WithMessage($"Quantity must be between 1 and {CartMergePlanner.MaxQuantityPerLine}.");

        RuleFor(x => x.ProductVariantId)
            .GreaterThan(0).When(x => x.ProductVariantId.HasValue)
            .WithMessage("ProductVariantId must be greater than 0 when supplied.");
    }
}

public sealed class UpdateCartItemRequestValidator : AbstractValidator<UpdateCartItemRequest>
{
    public UpdateCartItemRequestValidator() =>
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, CartMergePlanner.MaxQuantityPerLine)
            .WithMessage($"Quantity must be between 1 and {CartMergePlanner.MaxQuantityPerLine}.");
}

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(x => x.ShippingMethod)
            .NotEmpty().WithMessage("Shipping method is required.")
            .Must(m => ShippingOptions.IsValid(m))
            .WithMessage("Shipping method must be Standard or Express.");

        RuleFor(x => x.ShippingAddress).NotNull().WithMessage("Shipping address is required.");
        RuleFor(x => x.ShippingAddress).SetValidator(new AddressInputValidator());

        When(x => !x.BillingSameAsShipping, () =>
        {
            RuleFor(x => x.BillingAddress).NotNull().WithMessage("Billing address is required.");
            RuleFor(x => x.BillingAddress).SetValidator(new AddressInputValidator());
        });
    }
}

public sealed class AddressInputValidator : AbstractValidator<AddressInput>
{
    public AddressInputValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Line2).MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateAddressRequestValidator : AbstractValidator<CreateAddressRequest>
{
    public CreateAddressRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Line2).MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

public sealed class UpdateAddressRequestValidator : AbstractValidator<UpdateAddressRequest>
{
    public UpdateAddressRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Line1).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Line2).MaximumLength(300);
        RuleFor(x => x.City).NotEmpty().MaximumLength(100);
        RuleFor(x => x.State).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PostalCode).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Country).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(50);
    }
}

public sealed class AdminProductRequestValidator : AbstractValidator<AdminProductRequest>
{
    public AdminProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(220);
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Price).GreaterThan(0).WithMessage("Price must be greater than 0.");
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0).WithMessage("Stock cannot be negative.");
        RuleFor(x => x.ImageUrl).MaximumLength(500);
        RuleFor(x => x.DiscountPercent)
            .InclusiveBetween(0, 90).When(x => x.DiscountPercent.HasValue)
            .WithMessage("Discount percent must be between 0 and 90.");
        RuleFor(x => x.RatingNum)
            .InclusiveBetween(0, 5).When(x => x.RatingNum.HasValue)
            .WithMessage("Rating must be between 0 and 5.");
        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("A category is required.");
    }
}

public sealed class AdminCategoryRequestValidator : AbstractValidator<AdminCategoryRequest>
{
    public AdminCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Slug).MaximumLength(140);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ParentId).GreaterThan(0).When(x => x.ParentId.HasValue);
    }
}

public sealed class UpdateOrderStatusRequestValidator : AbstractValidator<UpdateOrderStatusRequest>
{
    public UpdateOrderStatusRequestValidator() =>
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Status is required.")
            .Must(s => Core.Entities.OrderStatus.All.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Status must be one of: Pending, Paid, Shipped, Delivered, Cancelled.");
}