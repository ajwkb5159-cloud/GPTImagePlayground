using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

/// <summary>
/// One turn of the Gemini conversation. <c>role</c> is "user" or "model"; the <c>contents</c> array is
/// what makes multi-turn context native — history turns carry the images that were produced back then.
/// </summary>
internal class GeminiContent
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("parts")]
    public List<GeminiPart> Parts { get; set; } = [];

    public static GeminiContent User(params GeminiPart[] parts) => new() { Role = "user", Parts = [.. parts] };

    public static GeminiContent Model(params GeminiPart[] parts) => new() { Role = "model", Parts = [.. parts] };
}
