using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

internal class TokenDetails
{
    [JsonPropertyName("text_tokens")]
    public int TextTokens { get; set; }

    [JsonPropertyName("image_tokens")]
    public int ImageTokens { get; set; }

    /// <summary>Input tokens served from the provider's prompt cache, when it reports them.</summary>
    [JsonPropertyName("cached_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CachedTokens { get; set; }
}
