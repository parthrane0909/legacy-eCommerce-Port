using System.Text.RegularExpressions;

namespace Ecommerce.Core.Domain;

/// <summary>
/// Server-side password policy. Deliberately stricter than the legacy Identity 2 policy, which
/// accepted any 6 characters with no complexity requirement.
/// </summary>
public static partial class PasswordPolicy
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;

    [GeneratedRegex(@"[^A-Za-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphanumeric();

    public static bool IsValid(string? password)
    {
        if (string.IsNullOrWhiteSpace(password)
            || password.Length < MinimumLength
            || password.Length > MaximumLength)
        {
            return false;
        }

        // Each rule is checked separately so a password must satisfy all four character classes.
        return HasUpper(password)
               && HasLower(password)
               && HasDigit(password)
               && NonAlphanumeric().IsMatch(password);
    }

    /// <summary>Human-readable rules, surfaced by validation errors and the register form.</summary>
    public static IReadOnlyList<string> Rules =>
    [
        $"At least {MinimumLength} characters.",
        "Must contain an uppercase letter.",
        "Must contain a lowercase letter.",
        "Must contain a digit.",
        "Must contain a non-alphanumeric character.",
    ];

    private static bool HasUpper(string value) => value.Any(char.IsUpper);

    private static bool HasLower(string value) => value.Any(char.IsLower);

    private static bool HasDigit(string value) => value.Any(char.IsDigit);
}