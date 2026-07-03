using System.Globalization;
using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal sealed class ContextDecisionService
{
    private readonly TextSimilarityService _similarityService;

    public ContextDecisionService(TextSimilarityService similarityService)
    {
        _similarityService = similarityService;
    }

    public ContextDecision Decide(
        string prompt,
        Conversation conversation,
        IReadOnlyList<string> manuallyAttachedImagePaths)
    {
        var config = conversation.ContextConfig;
        var promptProfile = PromptProfile.From(prompt);
        var recentPrompts = conversation.GetRecentUserPrompts(config.MaxContextPrompts)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
        var recentImages = conversation.GetRecentGeneratedImagePaths(
            Math.Max(1, config.MaxContextImages));
        var hasManualImages = manuallyAttachedImagePaths.Count > 0;
        var hasRecentImages = recentImages.Count > 0;
        var maxSimilarity = recentPrompts.Count == 0
            ? 0
            : recentPrompts.Max(item => _similarityService.CalculateSimilarity(prompt, item));

        if (hasManualImages)
        {
            var injectText = config.EnablePromptEnhancement && ShouldInjectTextContext(promptProfile, maxSimilarity);
            var shouldAttachHistoryImages = config.AllowHistoryImagesWithManualAttachments
                && config.EnableReferenceDetection
                && hasRecentImages
                && maxSimilarity >= 0.45;
            return new ContextDecision
            {
                Intent = ContextIntent.EditSelectedImages,
                ShouldAttachRecentImages = shouldAttachHistoryImages,
                ShouldInjectTextContext = injectText,
                SelectedImagePaths = shouldAttachHistoryImages ? recentImages : [],
                SelectedTextContext = injectText ? BuildTextContext(conversation, recentPrompts) : [],
                DecisionReason = shouldAttachHistoryImages
                    ? "已使用手动上传图片，并额外引用相关历史图。"
                    : injectText
                    ? "已使用手动上传图片，并补充少量会话上下文。"
                    : "已使用手动上传图片，未额外混入历史图。",
                Confidence = 0.95,
            };
        }

        var attachScore = CalculateAttachScore(promptProfile, hasRecentImages, maxSimilarity);
        var threshold = Clamp((double)config.ContextAutoAttachThreshold, 0.1, 0.95);
        var shouldAttach = config.EnableReferenceDetection
            && hasRecentImages
            && attachScore >= threshold;
        var shouldInjectText = config.EnablePromptEnhancement
            && (shouldAttach || ShouldInjectTextContext(promptProfile, maxSimilarity));

        var intent = DetermineIntent(shouldAttach, shouldInjectText, promptProfile, maxSimilarity);
        return new ContextDecision
        {
            Intent = intent,
            ShouldAttachRecentImages = shouldAttach,
            ShouldInjectTextContext = shouldInjectText,
            SelectedImagePaths = shouldAttach ? recentImages : [],
            SelectedTextContext = shouldInjectText ? BuildTextContext(conversation, recentPrompts) : [],
            DecisionReason = BuildReason(shouldAttach, shouldInjectText, attachScore, threshold, maxSimilarity),
            Confidence = shouldAttach ? attachScore : Math.Max(0, 1 - attachScore),
        };
    }

    private static double CalculateAttachScore(
        PromptProfile profile,
        bool hasRecentImages,
        double maxSimilarity)
    {
        if (!hasRecentImages || profile.IsEmpty)
            return 0;

        double score = 0.28;
        if (profile.IsVeryShort)
            score += 0.28;
        else if (profile.IsShort)
            score += 0.16;

        if (profile.IsSparse)
            score += 0.18;

        if (profile.HasMultipleFragments)
            score += 0.16;

        if (maxSimilarity >= 0.62)
            score += 0.22;
        else if (maxSimilarity >= 0.38)
            score += 0.14;
        else if (maxSimilarity >= 0.22)
            score += 0.08;

        if (profile.LooksLikeCompleteStandalonePrompt && maxSimilarity < 0.22)
            score -= 0.26;

        return Clamp(score, 0, 1);
    }

    private static bool ShouldInjectTextContext(PromptProfile profile, double maxSimilarity)
    {
        if (profile.IsEmpty)
            return false;

        return maxSimilarity >= 0.22
            || profile.IsVeryShort
            || profile.HasMultipleFragments;
    }

    private static ContextIntent DetermineIntent(
        bool shouldAttach,
        bool shouldInjectText,
        PromptProfile profile,
        double maxSimilarity)
    {
        if (shouldAttach)
            return ContextIntent.EditRecentImage;

        if (shouldInjectText && maxSimilarity >= 0.22)
            return ContextIntent.ContinueFromConversation;

        if (profile.IsVeryShort)
            return ContextIntent.Ambiguous;

        return ContextIntent.StandaloneGeneration;
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

    private static string BuildReason(
        bool shouldAttach,
        bool shouldInjectText,
        double attachScore,
        double threshold,
        double maxSimilarity)
    {
        var similarityText = maxSimilarity.ToString("0.00", CultureInfo.InvariantCulture);
        var scoreText = attachScore.ToString("0.00", CultureInfo.InvariantCulture);
        var thresholdText = threshold.ToString("0.00", CultureInfo.InvariantCulture);

        if (shouldAttach && shouldInjectText)
            return $"已自动引用最近生成图，并补充会话上下文。附图评分 {scoreText}/{thresholdText}，历史相似度 {similarityText}。";

        if (shouldAttach)
            return $"已自动引用最近生成图。附图评分 {scoreText}/{thresholdText}，历史相似度 {similarityText}。";

        if (shouldInjectText)
            return $"已补充会话文本上下文，未自动附图。附图评分 {scoreText}/{thresholdText}，历史相似度 {similarityText}。";

        return $"按独立提示词生成，未自动附加历史上下文。附图评分 {scoreText}/{thresholdText}。";
    }

    private static double Clamp(double value, double min, double max) =>
        Math.Min(max, Math.Max(min, value));

    private sealed class PromptProfile
    {
        public bool IsEmpty { get; private init; }
        public bool IsVeryShort { get; private init; }
        public bool IsShort { get; private init; }
        public bool IsSparse { get; private init; }
        public bool HasMultipleFragments { get; private init; }
        public bool LooksLikeCompleteStandalonePrompt { get; private init; }

        public static PromptProfile From(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new PromptProfile { IsEmpty = true };

            var trimmed = prompt.Trim();
            var cjkCount = trimmed.Count(IsCjk);
            var latinWordCount = CountLatinWords(trimmed);
            var visualLength = cjkCount + latinWordCount;
            var fragmentCount = trimmed.Count(IsFragmentSeparator) + 1;
            var compactLength = trimmed.Count(ch => !char.IsWhiteSpace(ch));

            return new PromptProfile
            {
                IsVeryShort = visualLength <= 6 || compactLength <= 8,
                IsShort = visualLength <= 14 || compactLength <= 22,
                IsSparse = CountDistinctSignalUnits(trimmed) <= 5,
                HasMultipleFragments = fragmentCount >= 2,
                LooksLikeCompleteStandalonePrompt =
                    fragmentCount == 1
                    && compactLength >= 10
                    && CountDistinctSignalUnits(trimmed) >= 6,
            };
        }

        private static int CountLatinWords(string text) =>
            text.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Count(token => token.Any(ch => ch <= '\u007f' && char.IsLetterOrDigit(ch)));

        private static int CountDistinctSignalUnits(string text)
        {
            var units = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var ch in text)
            {
                if (IsCjk(ch))
                    units.Add(ch.ToString());
            }

            foreach (var token in text.Split(
                [' ', '\t', '\r', '\n', ',', '.', ';', ':', '，', '。', '；', '：', '、'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token.Any(char.IsLetterOrDigit))
                    units.Add(token);
            }

            return units.Count;
        }

        private static bool IsCjk(char ch) =>
            ch >= '\u4e00' && ch <= '\u9fff';

        private static bool IsFragmentSeparator(char ch) =>
            ch is ',' or '.' or ';' or ':' or '\n' or '\r'
                or '，' or '。' or '；' or '：' or '、';
    }
}
