using ImageGenerator.Models;

namespace ImageGenerator.Services;

/// <summary>
/// Decides how the ongoing conversation is folded into the next request. There is no keyword or
/// similarity heuristic: text context is always injected when the conversation has any, and the
/// newest generated image is always carried along as the edit base while "reuse last image" is on
/// (the model itself decides whether that reference applies to the current prompt).
/// </summary>
internal sealed class ContextDecisionService
{
    public ContextDecision Decide(
        string prompt,
        Conversation conversation,
        IReadOnlyList<string> manuallyAttachedImagePaths,
        bool reuseLastImage)
    {
        var config = conversation.ContextConfig;
        var recentPrompts = conversation.GetRecentUserPrompts(config.MaxContextPrompts)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        var textContext = BuildTextContext(conversation, recentPrompts);
        var injectText = textContext.Count > 0;

        if (manuallyAttachedImagePaths.Count > 0)
        {
            // Manually attached images define the edit base; the history image is not mixed in.
            return new ContextDecision
            {
                Intent = ContextIntent.EditSelectedImages,
                ShouldAttachRecentImages = false,
                ShouldInjectTextContext = injectText,
                SelectedImagePaths = [],
                SelectedTextContext = injectText ? textContext : [],
                DecisionReason = injectText
                    ? "已使用手动上传图片，并补充会话上下文。"
                    : "已使用手动上传图片。",
                Confidence = 0.95,
            };
        }

        var latestImagePath = conversation.GetLatestGeneratedImagePath();
        var imageExists = !string.IsNullOrWhiteSpace(latestImagePath) && File.Exists(latestImagePath);
        var attach = reuseLastImage && imageExists;

        return new ContextDecision
        {
            Intent = attach
                ? ContextIntent.EditRecentImage
                : injectText ? ContextIntent.ContinueFromConversation : ContextIntent.StandaloneGeneration,
            ShouldAttachRecentImages = attach,
            ShouldInjectTextContext = injectText,
            SelectedImagePaths = attach ? [latestImagePath!] : [],
            SelectedTextContext = injectText ? textContext : [],
            DecisionReason = BuildReason(attach, reuseLastImage, latestImagePath, imageExists, injectText),
            Confidence = attach ? 0.9 : 0.7,
        };
    }

    private static string BuildReason(
        bool attach,
        bool reuseLastImage,
        string? latestImagePath,
        bool imageExists,
        bool injectText)
    {
        if (attach)
            return "已把本会话最近生成的图片作为编辑基础（可在输入框上方取消）。";

        if (!reuseLastImage)
            return "「续接上一张图」已关闭，本轮按全新生成发送。";

        if (!string.IsNullOrWhiteSpace(latestImagePath) && !imageExists)
            return $"本会话最近生成的图片文件已不存在（{latestImagePath}），已按纯文本生成发送。";

        return injectText
            ? "本会话还没有可复用的生成图，已按独立提示词生成并补充会话上下文。"
            : "本会话还没有可复用的生成图，已按独立提示词生成。";
    }

    private static List<string> BuildTextContext(Conversation conversation, IReadOnlyList<string> recentPrompts)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(conversation.CompressedSummary))
            parts.Add($"会话摘要：{conversation.CompressedSummary}");

        if (recentPrompts.Count > 0)
            parts.Add("最近提示词：" + string.Join(" | ", recentPrompts));

        return parts;
    }
}
