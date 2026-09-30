using ImageGenerator.Models;

namespace ImageGenerator.Services;

/// <summary>
/// How much conversation context fits into the selected model's token window for the current
/// request. Produced before every generation and shown in the chat status line.
/// </summary>
internal sealed class ContextBudgetPlan
{
    public string Model { get; init; } = "";
    public int MaxContextTokens { get; init; }
    public int MaxOutputTokens { get; init; }
    public int InputBudget { get; init; }
    public int CurrentPromptTokens { get; init; }
    public int EstimatedHistoryTokens { get; init; }
    public int EstimatedInputTokens { get; init; }
    public int SelectedMessageCount { get; init; }
    public int SelectedPromptCount { get; init; }
    public int TrimmedMessageCount { get; init; }
    public bool PromptExceedsBudget { get; init; }
    public int? MeasuredInputTokens { get; init; }
}

/// <summary>
/// Derives the effective context window of a conversation from the token budget of the selected
/// model (<c>MaxContextTokens - MaxOutputTokens</c>) instead of the legacy message-count knobs.
/// The derived counts are written back into <see cref="Conversation.ContextConfig"/> so the
/// existing decision/compression services keep working unchanged but stay inside the real window.
/// </summary>
internal sealed class ContextBudgetPlanner
{
    private const int MinimumInputBudget = 512;
    private const int SafetyMarginTokens = 256;
    private const int MinImageTokensPerImage = 64;
    private const int MaxImageTokensPerImage = 8192;

    public ContextBudgetPlan Plan(
        Conversation conversation,
        AppConfig appConfig,
        string prompt,
        IReadOnlyList<string> manuallyAttachedImagePaths)
    {
        var contextConfig = conversation.ContextConfig ??= new ContextWindowConfig();
        var profile = ModelProfileStore.Resolve(appConfig, appConfig.Model);

        var maxContextTokens = Math.Max(ModelProfileStore.MinMaxContextTokens, profile.MaxContextTokens);
        var maxOutputTokens = Clamp(
            profile.MaxOutputTokens,
            ModelProfileStore.MinMaxOutputTokens,
            Math.Max(ModelProfileStore.MinMaxOutputTokens, maxContextTokens - 1));
        var inputBudget = Math.Max(MinimumInputBudget, maxContextTokens - maxOutputTokens);

        var imageTokensPerImage = ResolveImageTokensPerImage(conversation);
        var currentPromptTokens = TokenEstimator.EstimateText(prompt)
            + TokenEstimator.EstimateImageTokens(manuallyAttachedImagePaths.Count, imageTokensPerImage);
        var promptExceedsBudget = currentPromptTokens >= inputBudget;
        // SafetyMarginTokens covers the injected context prefix boilerplate and the optimism of
        // the local estimator, so a request never runs past the model window.
        var historyBudget = promptExceedsBudget
            ? 0
            : Math.Max(0, inputBudget - currentPromptTokens - SafetyMarginTokens);

        // The compressed summary is the oldest retained history and is size-capped by
        // ContextCompressor, so it is counted first and the message window gets what is left.
        var summaryTokens = string.IsNullOrWhiteSpace(conversation.CompressedSummary)
            ? 0
            : TokenEstimator.EstimateText(conversation.CompressedSummary);
        var messageBudget = Math.Max(0, historyBudget - summaryTokens);
        var remaining = messageBudget;

        var selectedMessages = 0;
        var selectedPrompts = 0;
        for (var i = conversation.Messages.Count - 1; i >= 0; i--)
        {
            var message = conversation.Messages[i];
            var cost = TokenEstimator.EstimateMessageContextTokens(message, imageTokensPerImage);
            if (cost > remaining)
                break;

            remaining -= cost;
            selectedMessages++;
            if (message.Role == ChatRole.User && !string.IsNullOrWhiteSpace(message.Prompt))
                selectedPrompts++;
        }

        // Always keep the newest message inside the active window, even when the budget is
        // already exhausted, so the conversation view and prompt injection stay coherent.
        var activeMessages = Math.Max(1, selectedMessages);
        var keepRecent = Math.Max(1, selectedMessages);
        var compressionTrigger = Math.Max(
            keepRecent + 1,
            keepRecent + Math.Max(2, keepRecent / 2));

        contextConfig.MaxContextTokens = maxContextTokens;
        contextConfig.MaxOutputTokens = maxOutputTokens;
        contextConfig.MaxActiveMessages = activeMessages;
        contextConfig.KeepRecentCount = keepRecent;
        contextConfig.CompressionTriggerCount = compressionTrigger;
        contextConfig.MaxContextPrompts = promptExceedsBudget ? 0 : selectedPrompts;

        var estimatedHistoryTokens = summaryTokens + Math.Max(0, messageBudget - remaining);
        return new ContextBudgetPlan
        {
            Model = profile.Model,
            MaxContextTokens = maxContextTokens,
            MaxOutputTokens = maxOutputTokens,
            InputBudget = inputBudget,
            CurrentPromptTokens = currentPromptTokens,
            EstimatedHistoryTokens = estimatedHistoryTokens,
            EstimatedInputTokens = currentPromptTokens + estimatedHistoryTokens,
            SelectedMessageCount = selectedMessages,
            SelectedPromptCount = contextConfig.MaxContextPrompts,
            TrimmedMessageCount = Math.Max(0, conversation.Messages.Count - activeMessages),
            PromptExceedsBudget = promptExceedsBudget,
            MeasuredInputTokens = FindLatestMeasuredInputTokens(conversation),
        };
    }

    /// <summary>
    /// Uses the measured image-token cost of the most recent successful request (when the API
    /// reports <c>input_tokens_details.image_tokens</c>) instead of the built-in fallback.
    /// </summary>
    private static int ResolveImageTokensPerImage(Conversation conversation)
    {
        for (var i = conversation.Messages.Count - 1; i >= 0; i--)
        {
            var message = conversation.Messages[i];
            var imageTokens = message.Usage?.InputTokensDetails?.ImageTokens ?? 0;
            if (message.Role != ChatRole.Assistant || imageTokens <= 0)
                continue;

            var imageCount = CountPrecedingAttachmentImages(conversation.Messages, i);
            if (imageCount <= 0)
                continue;

            return Clamp(imageTokens / imageCount, MinImageTokensPerImage, MaxImageTokensPerImage);
        }

        return TokenEstimator.DefaultImageTokens;
    }

    private static int CountPrecedingAttachmentImages(IReadOnlyList<ChatMessage> messages, int assistantIndex)
    {
        for (var i = assistantIndex - 1; i >= 0; i--)
        {
            if (messages[i].Role == ChatRole.User)
                return messages[i].AttachedImagePaths.Count;
        }

        return 0;
    }

    private static int? FindLatestMeasuredInputTokens(Conversation conversation)
    {
        for (var i = conversation.Messages.Count - 1; i >= 0; i--)
        {
            var usage = conversation.Messages[i].Usage;
            if (usage is { InputTokens: > 0 })
                return usage.InputTokens;
        }

        return null;
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));
}
