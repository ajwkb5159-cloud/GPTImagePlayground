using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal sealed class PromptEnhanceResult
{
    public string EnhancedPrompt { get; init; } = "";
    public List<string> AutoAttachedImagePaths { get; init; } = [];
    public ContextDecision? Decision { get; init; }
    public ContextBudgetPlan? Budget { get; init; }
}

internal class PromptEnhancer
{
    private readonly ContextDecisionService _decisionService;
    private readonly ContextBudgetPlanner _budgetPlanner;
    private AppConfig _config;

    public PromptEnhancer(
        ContextDecisionService decisionService,
        ContextBudgetPlanner budgetPlanner,
        AppConfig config)
    {
        _decisionService = decisionService;
        _budgetPlanner = budgetPlanner;
        _config = config;
    }

    /// <summary>
    /// Points the enhancer at the config produced by the settings dialog. The dialog returns a new
    /// <see cref="AppConfig"/> instance, so the reference has to be refreshed after every save.
    /// </summary>
    public void UpdateConfig(AppConfig config)
    {
        _config = config;
    }

    public PromptEnhanceResult Enhance(
        string prompt,
        Conversation conversation,
        IReadOnlyList<string> manuallyAttachedImagePaths)
    {
        // Size the context window from the selected model's token budget first; the decision then
        // only chooses which history (text and the latest image) travels with the request.
        var budget = _budgetPlanner.Plan(conversation, _config, prompt, manuallyAttachedImagePaths);
        var decision = _decisionService.Decide(
            prompt,
            conversation,
            manuallyAttachedImagePaths,
            _config.ReuseLastImage);
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
            Budget = budget,
        };
    }

    private static string BuildImageReferencePrompt(string prompt) =>
        "随请求附带的参考图是本会话最近生成的结果：只有当用户要求保持主体、构图、身份一致性，或要在其基础上修改时才以它为编辑基础；"
        + "如果用户要求的是一个全新画面，请忽略参考图并重新生成。"
        + Environment.NewLine
        + prompt;

    private static string BuildContextPrefix(IReadOnlyList<string> contextParts) =>
        "请基于以下本地会话上下文理解用户意图；如果当前用户提示词已经足够完整，请优先遵循当前提示词。"
        + Environment.NewLine
        + string.Join(Environment.NewLine, contextParts)
        + Environment.NewLine
        + "当前用户请求：";
}
