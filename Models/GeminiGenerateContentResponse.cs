using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

/// <summary>Response of the Gemini image interface; result images live inside <c>candidates[].content.parts[]</c>.</summary>
internal class GeminiGenerateContentResponse
{
    [JsonPropertyName("candidates")]
    public List<GeminiCandidate>? Candidates { get; set; }

    [JsonPropertyName("usageMetadata")]
    public GeminiUsageMetadata? UsageMetadata { get; set; }

    [JsonPropertyName("modelVersion")]
    public string? ModelVersion { get; set; }

    [JsonPropertyName("responseId")]
    public string? ResponseId { get; set; }

    /// <summary>Set when the request was rejected before generation (e.g. safety block).</summary>
    [JsonPropertyName("promptFeedback")]
    public GeminiPromptFeedback? PromptFeedback { get; set; }
}

internal class GeminiCandidate
{
    [JsonPropertyName("content")]
    public GeminiContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }
}

internal class GeminiPromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }
}

/// <summary>Token accounting of the Gemini interface; mapped onto <see cref="UsageInfo"/> for the chat monitor.</summary>
internal class GeminiUsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int TotalTokenCount { get; set; }

    [JsonPropertyName("cachedContentTokenCount")]
    public int? CachedContentTokenCount { get; set; }

    [JsonPropertyName("thoughtsTokenCount")]
    public int? ThoughtsTokenCount { get; set; }

    public UsageInfo ToUsageInfo() => new()
    {
        InputTokens = PromptTokenCount,
        OutputTokens = CandidatesTokenCount,
        TotalTokens = TotalTokenCount > 0 ? TotalTokenCount : PromptTokenCount + CandidatesTokenCount,
        CachedTokens = CachedContentTokenCount,
    };
}
