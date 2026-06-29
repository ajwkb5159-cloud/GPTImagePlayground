namespace ImageGenerator.Models;

internal enum ChatRole
{
    User,
    Assistant,
    System
}

internal class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> AttachedImagePaths { get; set; } = [];
    public string? GeneratedImagePath { get; set; }
    public string? GeneratedImageDataUrl { get; set; }
    public UsageInfo? Usage { get; set; }
    public string? ErrorText { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public static ChatMessage UserMessage(string prompt, List<string> imagePaths) => new()
    {
        Role = ChatRole.User,
        Prompt = prompt,
        AttachedImagePaths = imagePaths,
    };

    public static ChatMessage AssistantMessage(string prompt, string imagePath, string? dataUrl, UsageInfo? usage) => new()
    {
        Role = ChatRole.Assistant,
        Prompt = prompt,
        GeneratedImagePath = imagePath,
        GeneratedImageDataUrl = dataUrl,
        Usage = usage,
    };

    public static ChatMessage SystemMessage(string errorText) => new()
    {
        Role = ChatRole.System,
        ErrorText = errorText,
    };
}
