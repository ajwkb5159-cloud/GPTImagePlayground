namespace ImageGenerator.Services;

/// <summary>Which image-generation protocol a model speaks on this gateway.</summary>
internal enum ImageProtocol
{
    /// <summary>OpenAI-compatible image endpoints (stateless single prompt, optional reference images).</summary>
    OpenAiImages,

    /// <summary>
    /// Gemini native image interface (<c>:generateContent</c>): the whole conversation travels in
    /// <c>contents</c>, so history text *and* history images are real context for the model.
    /// </summary>
    GeminiGenerateContent,
}

/// <summary>
/// Picks the protocol from the model name. The gateway only exposes model ids, so the rule has to be
/// name based — and it is not cosmetic: asking the OpenAI image endpoints for a Gemini image model
/// fails with <c>not supported model for image generation, only imagen models are supported</c>.
/// </summary>
internal static class ImageProtocolResolver
{
    public static ImageProtocol Resolve(string? model) =>
        (model ?? "").Trim().StartsWith("gemini", StringComparison.OrdinalIgnoreCase)
            ? ImageProtocol.GeminiGenerateContent
            : ImageProtocol.OpenAiImages;

    /// <summary>True when the model is served through the Gemini multi-turn interface.</summary>
    public static bool UsesGeminiContext(string? model) =>
        Resolve(model) == ImageProtocol.GeminiGenerateContent;
}
