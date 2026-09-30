using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

/// <summary>
/// Token accounting as reported by the API. Cache fields are optional because most image models
/// do not report them: <c>null</c> means "not reported", which the chat monitor shows explicitly
/// instead of pretending the value is zero.
/// </summary>
internal class UsageInfo
{
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("input_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TokenDetails? InputTokensDetails { get; set; }

    /// <summary>OpenAI chat-completion style details, accepted as a second shape.</summary>
    [JsonPropertyName("prompt_tokens_details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TokenDetails? PromptTokensDetails { get; set; }

    [JsonPropertyName("cached_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CachedTokens { get; set; }

    /// <summary>Anthropic-style cache write tokens.</summary>
    [JsonPropertyName("cache_creation_input_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheCreationInputTokens { get; set; }

    /// <summary>Anthropic-style cache read tokens.</summary>
    [JsonPropertyName("cache_read_input_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CacheReadInputTokens { get; set; }

    /// <summary>Cache-hit input tokens, whichever detail shape the provider used.</summary>
    [JsonIgnore]
    public int? ReportedCachedTokens =>
        CachedTokens ?? InputTokensDetails?.CachedTokens ?? PromptTokensDetails?.CachedTokens;

    /// <summary>True when the provider reported any cache accounting for this request.</summary>
    [JsonIgnore]
    public bool HasCacheBreakdown =>
        ReportedCachedTokens.HasValue
        || CacheReadInputTokens.HasValue
        || CacheCreationInputTokens.HasValue;

    /// <summary>Input tokens that were billed as new (not served from cache).</summary>
    [JsonIgnore]
    public int UncachedInputTokens => Math.Max(0, InputTokens - (ReportedCachedTokens ?? 0));
}
