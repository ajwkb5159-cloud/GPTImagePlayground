using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal class ConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _storeDir;
    private readonly string _indexPath;

    public ConversationStore(string storeDir)
    {
        _storeDir = storeDir;
        _indexPath = Path.Combine(_storeDir, "index.json");
    }

    public string StoreDir => _storeDir;

    public async Task<List<ConversationMeta>> LoadIndexAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storeDir);

        try
        {
            if (File.Exists(_indexPath))
            {
                var json = await File.ReadAllTextAsync(_indexPath, cancellationToken).ConfigureAwait(false);
                var index = JsonSerializer.Deserialize<List<ConversationMeta>>(json, JsonOptions);
                if (index != null)
                    return index.OrderByDescending(meta => meta.UpdatedAt).ToList();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConversationStore] Failed to load index: {ex.Message}");
        }

        return await RebuildIndexAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Conversation?> LoadConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            return null;

        var path = GetConversationPath(conversationId);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Conversation>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ConversationStore] Failed to load conversation {conversationId}: {ex.Message}");
            TryBackupCorruptFile(path);
            return null;
        }
    }

    public async Task SaveConversationAsync(Conversation conversation, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storeDir);
        await WriteJsonAtomicallyAsync(GetConversationPath(conversation.Id), conversation, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task SaveIndexAsync(IEnumerable<ConversationMeta> index, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storeDir);
        var ordered = index.OrderByDescending(meta => meta.UpdatedAt).ToList();
        await WriteJsonAtomicallyAsync(_indexPath, ordered, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var path = GetConversationPath(conversationId);
        if (File.Exists(path))
            File.Delete(path);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<List<ConversationMeta>> RebuildIndexAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_storeDir);
        var metas = new List<ConversationMeta>();

        foreach (var file in Directory.EnumerateFiles(_storeDir, "*.json"))
        {
            if (string.Equals(Path.GetFileName(file), "index.json", StringComparison.OrdinalIgnoreCase))
                continue;

            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                var conversation = JsonSerializer.Deserialize<Conversation>(json, JsonOptions);
                if (conversation != null)
                    metas.Add(ConversationMeta.FromConversation(conversation));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ConversationStore] Skipping invalid conversation file {file}: {ex.Message}");
            }
        }

        metas = metas.OrderByDescending(meta => meta.UpdatedAt).ToList();
        await SaveIndexAsync(metas, cancellationToken).ConfigureAwait(false);
        return metas;
    }

    private string GetConversationPath(string conversationId) =>
        Path.Combine(_storeDir, $"{SanitizeFileName(conversationId)}.json");

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);

        if (File.Exists(path))
            File.Replace(tempPath, path, null);
        else
            File.Move(tempPath, path);
    }

    private static void TryBackupCorruptFile(string path)
    {
        try
        {
            var backupPath = $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(path, backupPath);
        }
        catch
        {
            // Best effort only.
        }
    }
}
