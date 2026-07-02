using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal sealed class PromptEnhanceResult
{
    public string EnhancedPrompt { get; init; } = "";
    public List<string> AutoAttachedImagePaths { get; init; } = [];
    public ReferenceStrength ReferenceStrength { get; init; }
}

internal class PromptEnhancer
{
    private readonly ReferenceDetector _referenceDetector;

    public PromptEnhancer(ReferenceDetector referenceDetector)
    {
        _referenceDetector = referenceDetector;
    }

    public PromptEnhanceResult Enhance(string prompt, Conversation conversation)
    {
        var config = conversation.ContextConfig;
        var detection = config.EnableReferenceDetection
            ? _referenceDetector.Detect(prompt)
            : new ReferenceDetectionResult { Strength = ReferenceStrength.None };

        var autoImages = new List<string>();
        if (detection.HasReference)
        {
            var lastImage = conversation.GetLastGeneratedImagePath();
            if (!string.IsNullOrWhiteSpace(lastImage))
                autoImages.Add(lastImage);
        }

        var enhancedPrompt = prompt;
        if (config.EnablePromptEnhancement && detection.HasReference)
        {
            var contextPrefix = BuildContextPrefix(conversation);
            if (!string.IsNullOrWhiteSpace(contextPrefix))
                enhancedPrompt = $"{contextPrefix}{Environment.NewLine}{prompt}";
        }

        return new PromptEnhanceResult
        {
            EnhancedPrompt = enhancedPrompt,
            AutoAttachedImagePaths = autoImages,
            ReferenceStrength = detection.Strength,
        };
    }

    private static string BuildContextPrefix(Conversation conversation)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(conversation.CompressedSummary))
            parts.Add($"上文摘要：{conversation.CompressedSummary}");

        var prompts = conversation.GetRecentUserPrompts(conversation.ContextConfig.MaxContextPrompts);
        if (prompts.Count > 0)
            parts.Add("最近的用户提示词：" + string.Join(" | ", prompts));

        if (parts.Count == 0)
            return "";

        return $"请仅将以下本地会话上下文用于保持连续性。{string.Join(" ", parts)}";
    }
}
