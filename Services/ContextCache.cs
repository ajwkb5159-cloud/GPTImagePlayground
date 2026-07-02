using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal class ContextCache
{
    private readonly int _capacity;
    private readonly Dictionary<string, LinkedListNode<Conversation>> _entries = [];
    private readonly LinkedList<Conversation> _lru = [];

    public ContextCache(int capacity = 8)
    {
        _capacity = Math.Max(1, capacity);
    }

    public bool TryGet(string conversationId, out Conversation? conversation)
    {
        if (!_entries.TryGetValue(conversationId, out var node))
        {
            conversation = null;
            return false;
        }

        _lru.Remove(node);
        _lru.AddFirst(node);
        conversation = node.Value;
        return true;
    }

    public void Set(Conversation conversation)
    {
        if (_entries.TryGetValue(conversation.Id, out var existing))
            _lru.Remove(existing);

        var node = new LinkedListNode<Conversation>(conversation);
        _lru.AddFirst(node);
        _entries[conversation.Id] = node;

        while (_entries.Count > _capacity && _lru.Last != null)
        {
            _entries.Remove(_lru.Last.Value.Id);
            _lru.RemoveLast();
        }
    }

    public void Remove(string conversationId)
    {
        if (!_entries.TryGetValue(conversationId, out var node))
            return;

        _lru.Remove(node);
        _entries.Remove(conversationId);
    }
}
