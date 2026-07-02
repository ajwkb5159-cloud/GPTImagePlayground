namespace ImageGenerator.Models;

internal enum ContextIntent
{
    StandaloneGeneration,
    ContinueFromConversation,
    EditRecentImage,
    EditSelectedImages,
    Ambiguous,
}

internal sealed class ContextDecision
{
    public ContextIntent Intent { get; init; }
    public bool ShouldAttachRecentImages { get; init; }
    public bool ShouldInjectTextContext { get; init; }
    public List<string> SelectedImagePaths { get; init; } = [];
    public List<string> SelectedTextContext { get; init; } = [];
    public string DecisionReason { get; init; } = "";
    public double Confidence { get; init; }
}
