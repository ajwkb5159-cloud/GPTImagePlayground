using System.Globalization;
using ImageGenerator.Models;

namespace ImageGenerator.Services;

/// <summary>Aggregated token accounting for one conversation.</summary>
internal sealed record UsageTotals(
    int Requests,
    int InputTokens,
    int OutputTokens,
    int TotalTokens,
    int? CachedTokens,
    int? CacheReadTokens,
    int? CacheWriteTokens);

/// <summary>
/// Numbers used by the chat token monitor. Text stays in the UI layer (localized keys for the
/// composer label, the bubble's own wording), so this only formats and aggregates values.
/// </summary>
internal static class UsageSummary
{
    public static UsageTotals Sum(IEnumerable<ChatMessage>? messages)
    {
        var requests = 0;
        var input = 0;
        var output = 0;
        var total = 0;
        int? cached = null;
        int? cacheRead = null;
        int? cacheWrite = null;

        if (messages != null)
        {
            foreach (var message in messages)
            {
                var usage = message.Usage;
                if (usage == null)
                    continue;

                requests++;
                input += usage.InputTokens;
                output += usage.OutputTokens;
                total += usage.TotalTokens;
                cached = Add(cached, usage.ReportedCachedTokens);
                cacheRead = Add(cacheRead, usage.CacheReadInputTokens);
                cacheWrite = Add(cacheWrite, usage.CacheCreationInputTokens);
            }
        }

        return new UsageTotals(requests, input, output, total, cached, cacheRead, cacheWrite);
    }

    public static string FormatCount(int value) =>
        value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>Formats an optional provider value; missing data reads as "not reported".</summary>
    public static string FormatOptional(int? value) =>
        value.HasValue ? FormatCount(value.Value) : "未上报";

    /// <summary>Cache-hit share of the input tokens, or null when nothing was reported.</summary>
    public static int? CacheHitPercent(int inputTokens, int? cachedTokens)
    {
        if (!cachedTokens.HasValue || inputTokens <= 0)
            return null;

        return Math.Clamp((int)Math.Round(cachedTokens.Value * 100.0 / inputTokens), 0, 100);
    }

    private static int? Add(int? current, int? value)
    {
        if (!value.HasValue)
            return current;

        return (current ?? 0) + value.Value;
    }
}
