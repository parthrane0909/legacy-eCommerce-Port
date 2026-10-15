using System.Globalization;
using System.Text;

namespace Ecommerce.Core.Domain;

/// <summary>URL-safe slug generation, ported from the legacy catalogue's slug convention.</summary>
public static class SlugGenerator
{
    public static string Generate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var lastWasDash = false;

        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                // Strip combining accents: "Crème" -> "Creme".
                continue;
            }

            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasDash = false;
            }
            else if (!lastWasDash && sb.Length > 0)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        return sb.ToString().Trim('-');
    }

    /// <summary>
    /// Appends a numeric suffix until the slug is unique. <paramref name="exists"/> is async because
    /// uniqueness is always checked against the database.
    /// </summary>
    public static async Task<string> EnsureUnique(string baseSlug, Func<string, Task<bool>> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);

        var slug = string.IsNullOrWhiteSpace(baseSlug) ? "item" : baseSlug;
        if (!await exists(slug))
        {
            return slug;
        }

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{slug}-{i}";
            if (!await exists(candidate))
            {
                return candidate;
            }
        }

        return $"{slug}-{Guid.NewGuid():N}";
    }
}