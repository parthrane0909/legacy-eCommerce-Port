using System.Globalization;

namespace Ecommerce.Core.Domain;

/// <summary>
/// Human-readable, collision-resistant order numbers.
/// </summary>
/// <remarks>
/// Replaces the legacy generator, which built <c>"ORD-" + timestamp + new Random().Next(...)</c> and
/// produced duplicates under concurrency (audit finding F-19). This uses a per-process random
/// component plus an incrementing counter, and the database additionally enforces uniqueness.
/// </remarks>
public static class OrderNumberGenerator
{
    private static long _counter;
    private static readonly Lock Gate = new();

    /// <summary>Format: ORD-20261014-000123-A7F2</summary>
    public static string Generate(DateTime utcNow, long? sequenceOverride = null)
    {
        long sequence;

        if (sequenceOverride.HasValue)
        {
            sequence = sequenceOverride.Value;
        }
        else
        {
            lock (Gate)
            {
                sequence = ++_counter;
            }
        }

        var suffix = Random.Shared.Next(0x1000, 0xFFFF).ToString("X4", CultureInfo.InvariantCulture);
        return $"ORD-{utcNow:yyyyMMdd}-{sequence:D6}-{suffix}";
    }

    /// <summary>Extracts the embedded date, used by tests and reporting.</summary>
    public static DateOnly ExtractDate(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
        {
            return default;
        }

        var parts = orderNumber.Split('-');
        if (parts.Length < 2 || parts[0] != "ORD" || parts[1].Length != 8
            || !DateOnly.TryParseExact(parts[1], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return default;
        }

        return date;
    }
}