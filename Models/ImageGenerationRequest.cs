using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

internal class ImageGenerationRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "gpt-image-2";

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = "";

    [JsonPropertyName("n")]
    public int? N { get; set; }

    [JsonPropertyName("size")]
    public string? Size { get; set; }

    [JsonPropertyName("quality")]
    public string? Quality { get; set; }

    [JsonPropertyName("style")]
    public string? Style { get; set; }

    // gwlink/NewAPI rejects string values for this field; send JSON boolean false.
    [JsonPropertyName("stream")]
    public bool? Stream { get; set; }

    [JsonPropertyName("background")]
    public string? Background { get; set; }

    [JsonPropertyName("moderation")]
    public string? Moderation { get; set; }

    [JsonPropertyName("user")]
    public string? User { get; set; }

    /// <summary>
    /// Optional base64 data URIs for reference images (used with /images/generations).
    /// When set, each entry should be "data:image/png;base64,..." format.
    /// </summary>
    [JsonPropertyName("image_urls")]
    public List<string>? ImageUrls { get; set; }
}
