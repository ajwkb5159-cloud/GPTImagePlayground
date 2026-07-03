using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal sealed class PromptEnhanceResult
{
    public string EnhancedPrompt { get; init; } = "";
    public List<string> AutoAttachedImagePaths { get; init; } = [];
    public ContextDecision? Decision { get; init; }
}

internal class PromptEnhancer
{
    private readonly ContextDecisionService _decisionService;

    public PromptEnhancer(ContextDecisionService decisionService)
    {
        _decisionService = decisionService;
    }

    public PromptEnhanceResult Enhance(
        string prompt,
        Conversation conversation,
        IReadOnlyList<string> manuallyAttachedImagePaths)
    {
        var decision = _decisionService.Decide(prompt, conversation, manuallyAttachedImagePaths);
        var autoImages = decision.SelectedImagePaths
            .Where(path => !manuallyAttachedImagePaths.Contains(path, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var enhancedPrompt = prompt;
        if (decision.ShouldAttachRecentImages)
            enhancedPrompt = BuildImageReferencePrompt(enhancedPrompt);

        if (decision.ShouldInjectTextContext && decision.SelectedTextContext.Count > 0)
        {
            var contextPrefix = BuildContextPrefix(decision.SelectedTextContext);
            if (!string.IsNullOrWhiteSpace(contextPrefix))
                enhancedPrompt = $"{contextPrefix}{Environment.NewLine}{enhancedPrompt}";
        }

        return new PromptEnhanceResult
        {
            EnhancedPrompt = enhancedPrompt,
            AutoAttachedImagePaths = autoImages,
            Decision = decision,
        };
    }

    private static string BuildImageReferencePrompt(string prompt) =>
        "请把随请求附带的参考图作为当前编辑基础。保留用户未要求改变的主体、构图、身份一致性和关键细节。"
        + Environment.NewLine
        + prompt;

    private static string BuildContextPrefix(IReadOnlyList<string> contextParts) =>
        "请基于以下本地会话上下文理解用户意图；如果当前用户提示词已经足够完整，请优先遵循当前提示词。"
        + Environment.NewLine
        + string.Join(Environment.NewLine, contextParts)
        + Environment.NewLine
        + "当前用户请求：";
}
