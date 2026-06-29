using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

internal class TokenDetails
{
    [JsonPropertyName("text_tokens")]
    public int TextTokens { get; set; }

    [JsonPropertyName("image_tokens")]
    public int ImageTokens { get; set; }
}
