using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

internal class AppConfig
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-image-2";
    public string OutputDir { get; set; } = "";
    public int TimeoutMinutes { get; set; } = 10;
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
}
