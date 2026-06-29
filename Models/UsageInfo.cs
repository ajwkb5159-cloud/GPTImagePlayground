using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

internal class UsageInfo
{
    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("input_tokens_details")]
    public TokenDetails? InputTokensDetails { get; set; }
}
