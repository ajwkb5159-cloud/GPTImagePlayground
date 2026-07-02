namespace ImageGenerator.Models;

internal class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "新会话";
    public List<ChatMessage> Messages { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public ContextWindowConfig ContextConfig { get; set; } = new();
    public string? CompressedSummary { get; set; }
    public int LastCompressedIndex { get; set; } = -1;

    public void AddMessage(ChatMessage message)
    {
        message.ConversationId = Id;
        Messages.Add(message);
        UpdatedAt = DateTime.Now;
    }

    public List<ChatMessage> GetActiveContextMessages()
    {
        var activeCount = Math.Max(1, ContextConfig.MaxActiveMessages);
        if (Messages.Count <= activeCount)
            return [.. Messages];

        var recent = Messages.Skip(Messages.Count - activeCount).ToList();
        if (!string.IsNullOrWhiteSpace(CompressedSummary))
        {
            recent.Insert(0, ChatMessage.SystemMessage($"上文摘要：{CompressedSummary}"));
        }

        return recent;
    }

    public List<string> GetRecentUserPrompts(int count) =>
        Messages
            .Where(message => message.Role == ChatRole.User && !string.IsNullOrWhiteSpace(message.Prompt))
            .Reverse()
            .Take(Math.Max(0, count))
            .Reverse()
            .Select(message => message.Prompt)
            .ToList();

    public List<string> GetRecentGeneratedImagePaths(int count)
    {
        var paths = new List<string>();
        for (var i = Messages.Count - 1; i >= 0 && paths.Count < count; i--)
        {
            var path = Messages[i].GeneratedImagePath;
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                paths.Add(path);
        }

        paths.Reverse();
        return paths;
    }
}
