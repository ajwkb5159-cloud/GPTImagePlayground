using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal class ContextCompressor
{
    public bool TryCompress(Conversation conversation)
    {
        var config = conversation.ContextConfig;
        var trigger = Math.Max(config.KeepRecentCount + 1, config.CompressionTriggerCount);
        if (conversation.Messages.Count <= trigger)
            return false;

        var keepRecent = Math.Max(1, config.KeepRecentCount);
        var compressUntilExclusive = conversation.Messages.Count - keepRecent;
        if (compressUntilExclusive <= conversation.LastCompressedIndex + 1)
            return false;

        var messagesToCompress = conversation.Messages
            .Take(compressUntilExclusive)
            .Skip(Math.Max(0, conversation.LastCompressedIndex + 1))
            .ToList();

        var summaryParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(conversation.CompressedSummary))
            summaryParts.Add(conversation.CompressedSummary);

        var userPrompts = messagesToCompress
            .Where(message => message.Role == ChatRole.User && !string.IsNullOrWhiteSpace(message.Prompt))
            .Select(message => message.Prompt.Trim())
            .TakeLast(8)
            .ToList();
        var imageCount = messagesToCompress.Count(message =>
            message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.GeneratedImagePath));

        if (userPrompts.Count > 0)
            summaryParts.Add("较早的提示词：" + string.Join(" | ", userPrompts));
        if (imageCount > 0)
            summaryParts.Add($"较早生成的图片数量：{imageCount}。");

        conversation.CompressedSummary = TrimSummary(string.Join(" ", summaryParts));
        conversation.LastCompressedIndex = compressUntilExclusive - 1;
        return true;
    }

    private static string TrimSummary(string summary)
    {
        const int maxLength = 1600;
        if (summary.Length <= maxLength)
            return summary;

        return "..." + summary[^maxLength..];
    }
}
