namespace ImageGenerator.Models;

/// <summary>
/// Per-model context capability. The token values are either typed by the user in the
/// context tab or prefilled from optional metadata returned by the model list endpoint.
/// </summary>
internal class ModelContextProfile
{
    public string Model { get; set; } = "";
    public int MaxContextTokens { get; set; } = 32768;
    public int MaxOutputTokens { get; set; } = 4096;
    public bool HasManualTokens { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
