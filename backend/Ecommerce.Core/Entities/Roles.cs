namespace Ecommerce.Core.Entities;

/// <summary>Authorisation role names.</summary>
public static class Roles
{
    public const string Customer = "Customer";
    public const string Admin = "Admin";

    public static readonly string[] All = [Customer, Admin];
}