using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal class ConversationManager
{
    private readonly ConversationStore _store;
    private readonly ContextCache _cache;
    private readonly ContextCompressor _compressor;
    private AppConfig _config;
    private readonly List<ConversationMeta> _conversationList = [];

    public ConversationManager(
        ConversationStore store,
        ContextCache cache,
        ContextCompressor compressor,
        AppConfig config)
    {
        _store = store;
        _cache = cache;
        _compressor = compressor;
        _config = config;
    }

    public IReadOnlyList<ConversationMeta> ConversationList => _conversationList;
    public Conversation? ActiveConversation { get; private set; }
    public string? ActiveConversationId => ActiveConversation?.Id;

    /// <summary>
    /// Replaces the config reference used for future context-window decisions and
    /// refreshes the active conversation's context config in place. Does not reload
    /// from disk or touch messages, so settings changes stay decoupled from
    /// generation and conversation switching.
    /// </summary>
    public void UpdateConfig(AppConfig config)
    {
        _config = config;
        if (ActiveConversation != null)
            ApplyConfigToConversation(ActiveConversation);
    }

    public async Task InitializeAsync()
    {
        _conversationList.Clear();
        _conversationList.AddRange(await _store.LoadIndexAsync().ConfigureAwait(false));

        if (_conversationList.Count == 0)
        {
            await CreateConversationAsync().ConfigureAwait(false);
            return;
        }

        var targetId = !string.IsNullOrWhiteSpace(_config.LastConversationId)
            && _conversationList.Any(meta => meta.Id == _config.LastConversationId)
                ? _config.LastConversationId
                : _conversationList[0].Id;

        await SwitchToConversationAsync(targetId!).ConfigureAwait(false);
    }

    public async Task<Conversation> CreateConversationAsync()
    {
        var conversation = new Conversation
        {
            ContextConfig = CreateContextConfigFromAppConfig(),
        };

        ActiveConversation = conversation!;
        _cache.Set(conversation);
        UpsertMeta(conversation);
        await PersistActiveAsync().ConfigureAwait(false);
        return conversation;
    }

    public async Task<Conversation?> SwitchToConversationAsync(string conversationId)
    {
        if (ActiveConversation?.Id == conversationId)
            return ActiveConversation;

        await SaveActiveConversationAsync().ConfigureAwait(false);

        if (!_cache.TryGet(conversationId, out var conversation))
        {
            conversation = await _store.LoadConversationAsync(conversationId).ConfigureAwait(false);
            if (conversation == null)
                return null;
            EnsureConversationConfig(conversation);
            _cache.Set(conversation);
        }

        var loadedConversation = conversation!;
        ActiveConversation = loadedConversation;
        _config.LastConversationId = loadedConversation.Id;
        return loadedConversation;
    }

    public async Task AddMessageToActiveConversationAsync(ChatMessage message)
    {
        if (ActiveConversation == null)
            await CreateConversationAsync().ConfigureAwait(false);

        ActiveConversation!.AddMessage(message);
        _compressor.TryCompress(ActiveConversation);
        UpsertMeta(ActiveConversation);
        await PersistActiveAsync().ConfigureAwait(false);
    }

    public async Task AddMessageToConversationAsync(string conversationId, ChatMessage message)
    {
        var conversation = await GetConversationAsync(conversationId).ConfigureAwait(false);
        if (conversation == null)
            return;

        conversation.AddMessage(message);
        _compressor.TryCompress(conversation);
        UpsertMeta(conversation);
        await _store.SaveConversationAsync(conversation).ConfigureAwait(false);
        await _store.SaveIndexAsync(_conversationList).ConfigureAwait(false);
    }

    public async Task SaveActiveConversationAsync()
    {
        if (ActiveConversation == null)
            return;

        UpsertMeta(ActiveConversation);
        await PersistActiveAsync().ConfigureAwait(false);
    }

    public async Task DeleteConversationAsync(string conversationId)
    {
        _cache.Remove(conversationId);
        _conversationList.RemoveAll(meta => meta.Id == conversationId);
        await _store.DeleteConversationAsync(conversationId).ConfigureAwait(false);

        if (ActiveConversation?.Id == conversationId)
            ActiveConversation = null;

        if (_conversationList.Count == 0)
            await CreateConversationAsync().ConfigureAwait(false);
        else
            await _store.SaveIndexAsync(_conversationList).ConfigureAwait(false);
    }

    public async Task RenameConversationAsync(string conversationId, string title)
    {
        var conversation = await GetConversationAsync(conversationId).ConfigureAwait(false);
        if (conversation == null)
            return;

        conversation.Title = string.IsNullOrWhiteSpace(title) ? "新会话" : title.Trim();
        conversation.UpdatedAt = DateTime.Now;
        UpsertMeta(conversation);
        await _store.SaveConversationAsync(conversation).ConfigureAwait(false);
        await _store.SaveIndexAsync(_conversationList).ConfigureAwait(false);
    }

    private async Task<Conversation?> GetConversationAsync(string conversationId)
    {
        if (ActiveConversation?.Id == conversationId)
            return ActiveConversation;

        if (_cache.TryGet(conversationId, out var cachedConversation))
            return cachedConversation;

        var conversation = await _store.LoadConversationAsync(conversationId).ConfigureAwait(false);
        if (conversation == null)
            return null;

        EnsureConversationConfig(conversation);
        _cache.Set(conversation);
        return conversation;
    }

    private async Task PersistActiveAsync()
    {
        if (ActiveConversation == null)
            return;

        _config.LastConversationId = ActiveConversation.Id;
        await _store.SaveConversationAsync(ActiveConversation).ConfigureAwait(false);
        await _store.SaveIndexAsync(_conversationList).ConfigureAwait(false);
    }

    private void UpsertMeta(Conversation conversation)
    {
        var meta = ConversationMeta.FromConversation(conversation);
        var index = _conversationList.FindIndex(item => item.Id == meta.Id);
        if (index >= 0)
            _conversationList[index] = meta;
        else
            _conversationList.Add(meta);

        _conversationList.Sort((left, right) => right.UpdatedAt.CompareTo(left.UpdatedAt));
    }

    private ContextWindowConfig CreateContextConfigFromAppConfig()
    {
        var profile = ModelProfileStore.Resolve(_config, _config.Model);
        return new ContextWindowConfig
        {
            MaxContextTokens = profile.MaxContextTokens,
            MaxOutputTokens = profile.MaxOutputTokens,
        };
    }

    private void EnsureConversationConfig(Conversation conversation)
    {
        conversation.ContextConfig ??= CreateContextConfigFromAppConfig();
        ApplyConfigToConversation(conversation);
    }

    /// <summary>
    /// Copies the token budget of the active model onto a conversation. The message-count limits
    /// are left alone: <c>ContextBudgetPlanner</c> derives them from that budget before each
    /// generation.
    /// </summary>
    private void ApplyConfigToConversation(Conversation conversation)
    {
        if (conversation.ContextConfig == null)
        {
            conversation.ContextConfig = CreateContextConfigFromAppConfig();
            return;
        }

        var profile = ModelProfileStore.Resolve(_config, _config.Model);
        conversation.ContextConfig.MaxContextTokens = profile.MaxContextTokens;
        conversation.ContextConfig.MaxOutputTokens = profile.MaxOutputTokens;
    }
}
