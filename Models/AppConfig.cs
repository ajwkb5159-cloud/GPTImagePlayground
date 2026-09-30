namespace ImageGenerator.Models;

internal class AppConfig
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-image-2";

    /// <summary>
    /// Context capability per model. The active model is <see cref="Model"/>; its profile supplies
    /// the token budget the conversation context is sized against.
    /// </summary>
    public List<ModelContextProfile> ModelProfiles { get; set; } = [];

    public string OutputDir { get; set; } = "";
    public int TimeoutMinutes { get; set; } = 10;
    public bool VerifySslCertificate { get; set; } = true;
    public string SizeMode { get; set; } = "auto";
    public string SizeTier { get; set; } = "1K";
    public string AspectRatio { get; set; } = "1:1";
    public int CustomWidth { get; set; } = 1024;
    public int CustomHeight { get; set; } = 1024;
    public string OutputFormat { get; set; } = "png";
    public bool TransparentBackground { get; set; }
    public string Moderation { get; set; } = "auto";
    public int ImageCount { get; set; } = 1;
    public bool UseConcurrentStrategy { get; set; } = true;
    public int MaxConcurrency { get; set; } = 4;
    public string ConversationStoreDir { get; set; } = "conversations";
    public string? LastConversationId { get; set; }

    /// <summary>
    /// When true (the default) the newest generated image of the active conversation travels with
    /// the next request as the edit base; the chat input exposes this as a checkbox.
    /// </summary>
    public bool ReuseLastImage { get; set; } = true;

    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "zh-CN";
}
