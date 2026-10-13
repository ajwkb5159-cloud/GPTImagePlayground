using ImageGenerator.Models;

namespace ImageGenerator.Services;

/// <summary>
/// Local, dependency-free token estimate used to size the conversation context against the
/// selected model's token budget. The API's own <c>usage</c> numbers stay the source of truth;
/// this estimator is deliberately approximate, while the per-image cost is calibrated from the
/// most recent measured response (see <c>ContextBudgetPlanner</c>).
/// </summary>
internal static class TokenEstimator
{
    /// <summary>Fallback cost for one attached reference image when nothing was measured yet.</summary>
    public const int DefaultImageTokens = 1024;

    private const double LatinTokensPerWord = 1.35;
    private const int LatinCharsPerToken = 4;

    public static int EstimateText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var cjkCount = 0;
        var latinChars = 0;
        var latinWords = 0;
        var inWord = false;

        foreach (var ch in text)
        {
            if (IsCjk(ch))
            {
                cjkCount++;
                inWord = false;
                continue;
            }

            if (ch <= '\u007f' && char.IsLetterOrDigit(ch))
            {
                latinChars++;
                if (!inWord)
                {
                    latinWords++;
                    inWord = true;
                }

                continue;
            }

            inWord = false;
        }

        var latinTokens = Math.Max(latinChars / (double)LatinCharsPerToken, latinWords * LatinTokensPerWord);
        return (int)Math.Ceiling(cjkCount + latinTokens);
    }

    /// <summary>A per-image cost of zero means the images are not sent at all, so they cost nothing.</summary>
    public static int EstimateImageTokens(int imageCount, int imageTokensPerImage) =>
        imageCount <= 0 || imageTokensPerImage <= 0 ? 0 : imageCount * imageTokensPerImage;

    /// <summary>
    /// Approximate cost this message contributes to the next request. Only what the app actually
    /// sends counts: user prompts and their reference images, plus the historical image that
    /// auto-attach may reuse. Assistant bubbles add no text because the app injects context as a
    /// prompt prefix instead of replaying the chat history, and system bubbles are UI-only.
    /// </summary>
    public static int EstimateMessageContextTokens(ChatMessage message, int imageTokensPerImage)
    {
        if (message == null)
            return 0;

        return message.Role switch
        {
            ChatRole.User => EstimateText(message.Prompt)
                + EstimateImageTokens(message.AttachedImagePaths.Count, imageTokensPerImage),
            ChatRole.Assistant => EstimateImageTokens(
                string.IsNullOrWhiteSpace(message.GeneratedImagePath) ? 0 : 1,
                imageTokensPerImage),
            _ => 0,
        };
    }

    /// <summary>
    /// Broad CJK coverage: Han (incl. ext-A), CJK punctuation, kana, hangul, fullwidth forms and
    /// halfwidth katakana all cost roughly one token per character, unlike Latin text. Without this
    /// the budget would treat Japanese/Korean prompts as free and overshoot the model window.
    /// </summary>
    private static bool IsCjk(char ch) =>
        (ch >= '\u3000' && ch <= '\u303f')    // CJK punctuation
        || (ch >= '\u3040' && ch <= '\u30ff') // hiragana + katakana
        || (ch >= '\u3400' && ch <= '\u4dbf') // CJK ext-A
        || (ch >= '\u4e00' && ch <= '\u9fff') // CJK unified ideographs
        || (ch >= '\uf900' && ch <= '\ufaff') // CJK compatibility ideographs
        || (ch >= '\ufe30' && ch <= '\ufe4f') // CJK compatibility forms
        || (ch >= '\uff00' && ch <= '\uffef') // fullwidth / halfwidth forms
        || (ch >= '\uac00' && ch <= '\ud7af'); // hangul syllables
}
