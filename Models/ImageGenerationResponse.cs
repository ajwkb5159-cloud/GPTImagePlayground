using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

internal class ImageGenerationResponse
{
    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("data")]
    public List<ImageDataItem>? Data { get; set; }

    [JsonPropertyName("usage")]
    public UsageInfo? Usage { get; set; }
}
