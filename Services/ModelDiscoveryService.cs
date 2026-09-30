using System.Text.Json;

namespace ImageGenerator.Services;

/// <summary>One model reported by the API's <c>/models</c> endpoint.</summary>
internal sealed record DiscoveredModel(string Id, int? MaxContextTokens, int? MaxOutputTokens);

/// <summary>
/// Reads the model list exposed by an OpenAI-compatible gateway. Only the model ids are
/// required; token metadata is treated as an optional hint because most gateways omit it.
/// </summary>
internal sealed class ModelDiscoveryService
{
    private const int ErrorPreviewLength = 200;

    public async Task<IReadOnlyList<DiscoveredModel>> GetModelsAsync(
        string baseUrl,
        string apiKey,
        bool verifySslCertificate,
        int timeoutMinutes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("API 地址不能为空。");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("API Key 不能为空。");

        var endpoint = $"{baseUrl.Trim().TrimEnd('/')}/models";

        using var httpClient = ApiHttpClientFactory.Create(
            apiKey.Trim(),
            verifySslCertificate,
            Math.Clamp(timeoutMinutes, 1, 60));

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(endpoint, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException($"获取模型超时：{endpoint}");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"无法连接 API 地址：{ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    response.StatusCode is System.Net.HttpStatusCode.Unauthorized
                        or System.Net.HttpStatusCode.Forbidden
                        ? $"API Key 无效或无权限（HTTP {(int)response.StatusCode}）。"
                        : $"获取模型失败（HTTP {(int)response.StatusCode} {response.ReasonPhrase}）。");
            }

            var models = ParseModels(body);
            if (models.Count == 0)
            {
                throw new InvalidOperationException(
                    $"接口未返回任何模型。响应预览：{Preview(body)}");
            }

            return models;
        }
    }

    private static List<DiscoveredModel> ParseModels(string body)
    {
        var models = new List<DiscoveredModel>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"接口未返回 JSON 格式的模型列表。响应预览：{Preview(body)}");
        }

        using (document)
        {
            var root = document.RootElement;
            var entries = ResolveEntries(root);
            if (entries == null)
            {
                throw new InvalidOperationException(
                    $"无法从响应中识别模型列表。响应预览：{Preview(body)}");
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries.Value.EnumerateArray())
            {
                var model = ReadModel(entry);
                if (model == null || model.Id.Length == 0 || !seen.Add(model.Id))
                    continue;

                models.Add(model);
            }
        }

        models.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.OrdinalIgnoreCase));
        return models;
    }

    /// <summary>Accepts <c>{"data":[…]}</c>, <c>{"models":[…]}</c>, or a bare array.</summary>
    private static JsonElement? ResolveEntries(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root;

        if (root.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var propertyName in new[] { "data", "models", "model_list" })
        {
            if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Array)
                return value;
        }

        return null;
    }

    private static DiscoveredModel? ReadModel(JsonElement entry)
    {
        if (entry.ValueKind == JsonValueKind.String)
            return new DiscoveredModel((entry.GetString() ?? "").Trim(), null, null);

        if (entry.ValueKind != JsonValueKind.Object)
            return null;

        var id = ReadString(entry, "id")
            ?? ReadString(entry, "name")
            ?? ReadString(entry, "model");
        if (id == null)
            return null;

        return new DiscoveredModel(
            id.Trim(),
            ReadInt(entry, "context_length", "context_window", "max_context_tokens", "max_input_tokens", "max_model_len"),
            ReadInt(entry, "max_output_tokens", "max_completion_tokens"));
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? ReadInt(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var value))
                continue;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String
                && int.TryParse(value.GetString(), out var parsed))
                return parsed;
        }

        return null;
    }

    private static string Preview(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0)
            return "(空响应)";

        return trimmed.Length > ErrorPreviewLength
            ? trimmed[..ErrorPreviewLength] + "..."
            : trimmed;
    }
}
