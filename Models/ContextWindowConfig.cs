namespace ImageGenerator.Models;

/// <summary>
/// Effective context window for one conversation. <see cref="MaxActiveMessages"/>,
/// <see cref="CompressionTriggerCount"/>, <see cref="KeepRecentCount"/> and
/// <see cref="MaxContextPrompts"/> are derived from the selected model's token budget by
/// <c>ContextBudgetPlanner</c> before every request, so they describe what still fits rather
/// than user-typed knobs.
/// </summary>
internal class ContextWindowConfig
{
    public int MaxContextTokens { get; set; } = 32768;
    public int MaxOutputTokens { get; set; } = 4096;
    public int MaxActiveMessages { get; set; } = 20;
    public int CompressionTriggerCount { get; set; } = 30;
    public int KeepRecentCount { get; set; } = 10;
    public int MaxContextPrompts { get; set; } = 5;
}
