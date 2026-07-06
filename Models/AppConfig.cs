namespace ImageGenerator.Models;

internal class AppConfig
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-image-2";
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
    public int MaxActiveMessages { get; set; } = 20;
    public int CompressionTriggerCount { get; set; } = 30;
    public int KeepRecentCount { get; set; } = 10;
    public int MaxContextPrompts { get; set; } = 5;
    public int MaxContextImages { get; set; } = 1;
    public decimal ContextAutoAttachThreshold { get; set; } = 0.55M;
    public bool ShowContextDecisionHint { get; set; } = true;
    public bool AllowHistoryImagesWithManualAttachments { get; set; }
    public bool EnableReferenceDetection { get; set; } = true;
    public bool EnablePromptEnhancement { get; set; } = true;
    public string Theme { get; set; } = "light";
    public string Language { get; set; } = "zh-CN";
}
