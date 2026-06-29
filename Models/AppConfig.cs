using System.Text.Json.Serialization;

namespace ImageGenerator.Models;

internal class AppConfig
{
    public string BaseUrl { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string Model { get; set; } = "gpt-image-2";
    public string OutputDir { get; set; } = "";
    public int TimeoutMinutes { get; set; } = 10;
}
