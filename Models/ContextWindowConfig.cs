namespace ImageGenerator.Models;

internal class ContextWindowConfig
{
    public int MaxActiveMessages { get; set; } = 20;
    public int CompressionTriggerCount { get; set; } = 30;
    public int KeepRecentCount { get; set; } = 10;
    public int MaxContextPrompts { get; set; } = 5;
    public int MaxContextImages { get; set; } = 3;
    public bool EnableReferenceDetection { get; set; } = true;
    public bool EnablePromptEnhancement { get; set; } = true;
}
