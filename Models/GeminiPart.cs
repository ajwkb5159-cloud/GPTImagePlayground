using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

/// <summary>One content part of a Gemini turn: text, an inline image, or a file reference.</summary>
internal class GeminiPart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("inlineData")]
    public GeminiInlineData? InlineData { get; set; }

    [JsonPropertyName("fileData")]
    public GeminiFileData? FileData { get; set; }

    public static GeminiPart FromText(string text) => new() { Text = text };

    public static GeminiPart FromImage(string mimeType, string base64Data) => new()
    {
        InlineData = new GeminiInlineData { MimeType = mimeType, Data = base64Data },
    };
}

/// <summary>Input image payload. <c>data</c> must be pure base64 (no <c>data:</c> URL prefix).</summary>
internal class GeminiInlineData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "image/png";

    [JsonPropertyName("data")]
    public string Data { get; set; } = "";
}

/// <summary>Result image reference used when the gateway returns a URL instead of inline base64.</summary>
internal class GeminiFileData
{
    [JsonPropertyName("mimeType")]
    public string? MimeType { get; set; }

    [JsonPropertyName("fileUri")]
    public string? FileUri { get; set; }
}
