namespace ImageGenerator.Models;

internal class ConversationMeta
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int MessageCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool HasGeneratedImages { get; set; }
    public string DisplayTime => UpdatedAt.ToString("MM-dd HH:mm");

    public static ConversationMeta FromConversation(Conversation conversation) => new()
    {
        Id = conversation.Id,
        Title = conversation.Title,
        MessageCount = conversation.Messages.Count,
        CreatedAt = conversation.CreatedAt,
        UpdatedAt = conversation.UpdatedAt,
        HasGeneratedImages = conversation.Messages.Any(message =>
            message.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(message.GeneratedImagePath)),
    };
}
