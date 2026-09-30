using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

/// <summary>
/// Body of <c>POST {BaseUrl}/v1beta/models/{model}:generateContent</c> — the gateway's native Gemini
/// image interface. Unlike the OpenAI image endpoints this one is stateless-free: the whole
/// conversation travels in <see cref="Contents"/>.
/// </summary>
internal class GeminiGenerateContentRequest
{
    [JsonPropertyName("contents")]
    public List<GeminiContent> Contents { get; set; } = [];

    [JsonPropertyName("generationConfig")]
    public GeminiGenerationConfig? GenerationConfig { get; set; }

    /// <summary>Gateway extension: "url" asks for a link instead of inline base64 (may be ignored).</summary>
    [JsonPropertyName("response_format")]
    public string? ResponseFormat { get; set; }
}

internal class GeminiGenerationConfig
{
    /// <summary>Must contain IMAGE; ["IMAGE"] when only the picture is wanted.</summary>
    [JsonPropertyName("responseModalities")]
    public List<string> ResponseModalities { get; set; } = ["IMAGE"];

    [JsonPropertyName("imageConfig")]
    public GeminiImageConfig? ImageConfig { get; set; }
}

/// <summary>Gemini image models take a ratio (not exact pixels).</summary>
internal class GeminiImageConfig
{
    [JsonPropertyName("aspectRatio")]
    public string? AspectRatio { get; set; }
}
