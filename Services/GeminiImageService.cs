using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using ImageGenerator.Models;

namespace ImageGenerator.Services;

/// <summary>
/// Image generation through the gateway's native Gemini interface
/// (<c>POST {BaseUrl}/v1beta/models/{model}:generateContent</c>).
///
/// Why a separate service: that endpoint takes a <c>contents</c> array, so the whole conversation —
/// every earlier user request *and* every image produced back then — is real context for the model.
/// The OpenAI image endpoints only take a single prompt and reject Gemini image models outright.
/// </summary>
internal sealed class GeminiImageService
{
    /// <summary>History turns sent as context. The token budget caps this further.</summary>
    public const int MaxHistoryTurns = 6;

    /// <summary>History images attached as context; each one costs image tokens.</summary>
    public const int MaxHistoryImages = 6;

    private const int MaxReferenceImageBytes = 12 * 1024 * 1024;

    private static readonly string[] SupportedAspectRatios =
        ["1:1", "2:3", "3:2", "3:4", "4:3", "4:5", "5:4", "9:16", "16:9", "21:9"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly AppConfig _config;
    private readonly GeneratedImageSaver _saver;
    private readonly ContextBudgetPlanner _budgetPlanner = new();

    public GeminiImageService(AppConfig config)
    {
        _config = config;
        _saver = new GeneratedImageSaver(config);
    }

    /// <summary>
    /// Generates image(s) with the conversation as context. <paramref name="conversation"/> is expected
    /// to already contain the current user message as its last entry (that is how the chat flow works);
    /// everything before it becomes the multi-turn history.
    /// </summary>
    public async Task<GenerateResult> GenerateAsync(
        string prompt,
        IReadOnlyList<string> attachedImagePaths,
        Conversation? conversation,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        _saver.EnsureOutputDirectory();

        // The token budget decides how much history travels; the planner also writes the derived
        // window into the conversation, which BuildHistory then reads.
        ContextBudgetPlan? plan = null;
        if (conversation != null)
        {
            plan = _budgetPlanner.Plan(conversation, _config, prompt, attachedImagePaths);
        }

        var history = BuildHistory(conversation);
        var currentTurn = await BuildCurrentTurnAsync(prompt, attachedImagePaths, cancellationToken)
            .ConfigureAwait(false);

        var contents = new List<GeminiContent>(history.Turns) { currentTurn };
        var aspectRatio = ResolveAspectRatio();
        var trimmedNote = plan is { TrimmedMessageCount: > 0 }
            ? $"，较早历史已按预算裁剪 {plan.TrimmedMessageCount} 条"
            : "";
        progress?.Report(
            $"Gemini 原生多轮上下文：携带 {history.UserTurnCount} 轮历史、{history.ImageCount} 张历史图{trimmedNote}，"
            + $"比例 {aspectRatio}，正在请求 API...");

        using var httpClient = ApiHttpClientFactory.Create(
            _config.ApiKey,
            _config.VerifySslCertificate,
            _config.TimeoutMinutes);

        var totalCount = Math.Max(1, _config.ImageCount);
        var maxConcurrency = _config.UseConcurrentStrategy
            ? Clamp(_config.MaxConcurrency, 1, Math.Min(5, totalCount))
            : 1;

        var sw = Stopwatch.StartNew();
        var results = new SubResult[totalCount];

        if (maxConcurrency <= 1)
        {
            for (var i = 0; i < totalCount; i++)
                results[i] = await RunSubRequestAsync(httpClient, contents, aspectRatio, totalCount, i, progress, cancellationToken)
                    .ConfigureAwait(false);
        }
        else
        {
            using var semaphore = new SemaphoreSlim(maxConcurrency);
            var tasks = Enumerable.Range(0, totalCount)
                .Select(async index =>
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        results[index] = await RunSubRequestAsync(
                                httpClient, contents, aspectRatio, totalCount, index, progress, cancellationToken)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })
                .ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        sw.Stop();

        var succeeded = results.Where(result => result is { Success: true, Image: not null })
            .Select(result => result!)
            .ToList();
        var failedMessages = results
            .Select((result, index) => new { Result = result, Index = index })
            .Where(item => item.Result?.Success != true)
            .Select(item => item.Result?.ErrorMessage ?? $"子请求 {item.Index + 1}/{totalCount}: 未返回结果")
            .ToList();

        if (succeeded.Count == 0)
        {
            throw new InvalidOperationException(
                "所有子请求均失败，未生成可保存的图片。" + Environment.NewLine + string.Join(Environment.NewLine, failedMessages));
        }

        var images = succeeded.Select(result => result.Image!).ToList();
        var savedPaths = await _saver
            .SaveAsync(images, forceIndexedNames: totalCount > 1, progress, cancellationToken)
            .ConfigureAwait(false);

        return new GenerateResult
        {
            SavedPaths = savedPaths,
            DataUrls = images
                .Select(image => $"data:{image.MimeType};base64,{Convert.ToBase64String(image.Bytes)}")
                .ToList(),
            Usage = AggregateUsage(succeeded.Select(result => result.Usage)),
            ServerTimeSeconds = succeeded.Max(result => result.ServerTimeSeconds),
            TotalTimeSeconds = sw.Elapsed.TotalSeconds,
            SuccessCount = savedPaths.Count,
            TotalCount = totalCount,
            FailedMessages = failedMessages,
        };
    }

    // ═══════════════════════════════════════════════════
    //  Conversation → contents
    // ═══════════════════════════════════════════════════

    private sealed record HistoryPayload(List<GeminiContent> Turns, int UserTurnCount, int ImageCount);

    /// <summary>
    /// Turns earlier messages into Gemini turns: each user request becomes a "user" turn with the
    /// reference images it carried, and each generated image becomes a "model" turn. This is what the
    /// model actually "remembers" — nothing is compressed or paraphrased.
    /// </summary>
    private HistoryPayload BuildHistory(Conversation? conversation)
    {
        var turns = new List<GeminiContent>();
        if (conversation == null || conversation.Messages.Count == 0)
            return new HistoryPayload(turns, 0, 0);

        var turnBudget = Math.Max(0, Math.Min(MaxHistoryTurns, conversation.ContextConfig.MaxContextPrompts));
        var imageBudget = MaxHistoryImages;
        var userTurns = 0;
        var images = 0;

        // The last message is the current request (added by MainForm right before generating), so
        // history starts one entry earlier.
        var lastIndex = conversation.Messages.Count - 1;
        if (conversation.Messages[lastIndex].Role == ChatRole.User)
            lastIndex--;

        for (var i = lastIndex; i >= 0; i--)
        {
            var message = conversation.Messages[i];
            if (message.Role == ChatRole.Assistant)
            {
                var path = message.GeneratedImagePath;
                if (images < imageBudget
                    && !string.IsNullOrWhiteSpace(path)
                    && File.Exists(path)
                    && TryReadImagePart(path, out var part))
                {
                    turns.Add(GeminiContent.Model(part!));
                    images++;
                }

                continue;
            }

            if (message.Role != ChatRole.User)
                continue;

            if (userTurns >= turnBudget)
                break;

            var parts = new List<GeminiPart>();
            if (!string.IsNullOrWhiteSpace(message.Prompt))
                parts.Add(GeminiPart.FromText(message.Prompt.Trim()));

            foreach (var attachment in message.AttachedImagePaths)
            {
                if (images >= imageBudget)
                    break;

                if (File.Exists(attachment) && TryReadImagePart(attachment, out var part))
                {
                    parts.Add(part!);
                    images++;
                }
            }

            if (parts.Count == 0)
                continue;

            turns.Add(GeminiContent.User([.. parts]));
            userTurns++;
        }

        turns.Reverse();
        return new HistoryPayload(turns, userTurns, images);
    }

    private async Task<GeminiContent> BuildCurrentTurnAsync(
        string prompt,
        IReadOnlyList<string> attachedImagePaths,
        CancellationToken cancellationToken)
    {
        var parts = new List<GeminiPart> { GeminiPart.FromText(prompt) };

        foreach (var path in attachedImagePaths)
        {
            if (!File.Exists(path))
                continue;

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            if (bytes.Length > MaxReferenceImageBytes)
                continue;

            parts.Add(GeminiPart.FromImage(GetMimeType(path), Convert.ToBase64String(bytes)));
        }

        return GeminiContent.User([.. parts]);
    }

    private static bool TryReadImagePart(string path, out GeminiPart? part)
    {
        part = null;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxReferenceImageBytes)
                return false;

            var bytes = File.ReadAllBytes(path);
            part = GeminiPart.FromImage(GetMimeType(path), Convert.ToBase64String(bytes));
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    // ═══════════════════════════════════════════════════
    //  Request / response
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Resolves the Gemini endpoint. The Gemini API lives at the gateway root (<c>/v1beta/...</c>),
    /// while the configured BaseUrl carries the OpenAI <c>/v1</c> prefix — so that suffix is dropped
    /// instead of being concatenated (which would produce <c>/v1/v1beta/...</c>).
    /// </summary>
    public static string ResolveGeminiEndpoint(string baseUrl, string model)
    {
        var root = (baseUrl ?? "").Trim().TrimEnd('/');
        if (root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            root = root[..^3].TrimEnd('/');

        return $"{root}/v1beta/models/{model}:generateContent";
    }

    private async Task<SubResult> RunSubRequestAsync(
        HttpClient httpClient,
        List<GeminiContent> contents,
        string aspectRatio,
        int totalCount,
        int index,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var requestSw = Stopwatch.StartNew();
        try
        {
            if (totalCount > 1)
                progress?.Report($"正在请求 API ({index + 1}/{totalCount})...");

            var request = new GeminiGenerateContentRequest
            {
                Contents = contents,
                GenerationConfig = new GeminiGenerationConfig
                {
                    ResponseModalities = ["IMAGE"],
                    ImageConfig = new GeminiImageConfig { AspectRatio = aspectRatio },
                },
                ResponseFormat = "url",
            };

            var endpoint = ResolveGeminiEndpoint(_config.BaseUrl, _config.Model);
            using var content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");
            using var response = await httpClient.PostAsync(endpoint, content, cancellationToken)
                .ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var serverTime = requestSw.Elapsed.TotalSeconds;

            if (!response.IsSuccessStatusCode)
            {
                return SubResult.Fail(DescribeHttpError(response.StatusCode, body, endpoint));
            }

            var parsed = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(body);
            if (parsed?.PromptFeedback?.BlockReason is { Length: > 0 } blockReason)
                return SubResult.Fail($"请求被 Gemini 安全策略拦截：{blockReason}");

            var image = await ExtractImageAsync(httpClient, parsed, progress, cancellationToken)
                .ConfigureAwait(false);
            if (image == null)
            {
                var text = ExtractText(parsed);
                return SubResult.Fail(
                    "Gemini 响应里没有图片"
                    + (string.IsNullOrWhiteSpace(text) ? "。" : $"，模型返回的文本：{Trim(text, 300)}"));
            }

            return new SubResult
            {
                Success = true,
                Image = image,
                Usage = parsed?.UsageMetadata?.ToUsageInfo(),
                ServerTimeSeconds = serverTime,
            };
        }
        catch (OperationCanceledException)
        {
            return SubResult.Fail("请求已取消或超时");
        }
        catch (Exception ex)
        {
            return SubResult.Fail(ex.Message);
        }
    }

    private static async Task<GeneratedImageData?> ExtractImageAsync(
        HttpClient httpClient,
        GeminiGenerateContentResponse? parsed,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in parsed?.Candidates ?? [])
        {
            foreach (var part in candidate.Content?.Parts ?? [])
            {
                if (!string.IsNullOrWhiteSpace(part.InlineData?.Data))
                {
                    try
                    {
                        return new GeneratedImageData(
                            Convert.FromBase64String(part.InlineData.Data),
                            string.IsNullOrWhiteSpace(part.InlineData.MimeType)
                                ? "image/png"
                                : part.InlineData.MimeType);
                    }
                    catch (FormatException)
                    {
                        // fall through to other parts
                    }
                }

                if (!string.IsNullOrWhiteSpace(part.FileData?.FileUri))
                {
                    progress?.Report("正在下载图片...");
                    try
                    {
                        var bytes = await httpClient.GetByteArrayAsync(part.FileData.FileUri, cancellationToken)
                            .ConfigureAwait(false);
                        return new GeneratedImageData(
                            bytes,
                            string.IsNullOrWhiteSpace(part.FileData.MimeType)
                                ? "image/png"
                                : part.FileData.MimeType);
                    }
                    catch (HttpRequestException)
                    {
                        // fall through to other parts
                    }
                }
            }
        }

        return null;
    }

    private static string ExtractText(GeminiGenerateContentResponse? parsed)
    {
        var texts = (parsed?.Candidates ?? [])
            .SelectMany(candidate => candidate.Content?.Parts ?? [])
            .Select(part => part.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return texts.Count == 0 ? "" : string.Join(" ", texts!);
    }

    private static string DescribeHttpError(HttpStatusCode status, string body, string endpoint)
    {
        var message = TryReadErrorMessage(body);
        var detail = string.IsNullOrWhiteSpace(message) ? Trim(body, 400) : message;

        return status switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                $"HTTP {(int)status}：API Key 无效或没有该模型/分组的权限。{detail}",
            HttpStatusCode.TooManyRequests =>
                $"HTTP 429：额度不足或触发限速。{detail}",
            HttpStatusCode.BadRequest =>
                $"HTTP 400：请求不符合 Gemini 图片接口要求。{detail}",
            _ => $"HTTP {(int)status}：{detail}\n端点：{endpoint}",
        };
    }

    /// <summary>Gateway errors are new-api JSON: <c>{"error":{"message":"..."}}</c>.</summary>
    private static string TryReadErrorMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "";

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString() ?? "";

                if (error.TryGetProperty("message", out var message))
                    return message.GetString() ?? "";
            }
        }
        catch (JsonException)
        {
            // not JSON: fall back to the raw body
        }

        return Trim(body, 400);
    }

    // ═══════════════════════════════════════════════════
    //  Size / helpers
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// Gemini image models only take an aspect ratio, so the configured size is reduced to the closest
    /// supported ratio (auto/exact pixel values cannot be honoured on this interface).
    /// </summary>
    private string ResolveAspectRatio()
    {
        if (string.Equals(_config.SizeMode, "preset", StringComparison.OrdinalIgnoreCase)
            && SupportedAspectRatios.Contains(_config.AspectRatio))
        {
            return _config.AspectRatio;
        }

        if (string.Equals(_config.SizeMode, "custom", StringComparison.OrdinalIgnoreCase)
            && _config.CustomWidth > 0 && _config.CustomHeight > 0)
        {
            return FindClosestRatio(_config.CustomWidth, _config.CustomHeight);
        }

        return "1:1";
    }

    private static string FindClosestRatio(int width, int height)
    {
        var target = width / (double)height;
        var best = "1:1";
        var bestDelta = double.MaxValue;

        foreach (var ratio in SupportedAspectRatios)
        {
            var parts = ratio.Split(':');
            if (parts.Length != 2
                || !double.TryParse(parts[0], out var w)
                || !double.TryParse(parts[1], out var h)
                || h == 0)
            {
                continue;
            }

            var delta = Math.Abs(target - w / h);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = ratio;
            }
        }

        return best;
    }

    private static UsageInfo? AggregateUsage(IEnumerable<UsageInfo?> usages)
    {
        var list = usages.Where(usage => usage != null).Cast<UsageInfo>().ToList();
        if (list.Count == 0)
            return null;

        var cached = list.Any(usage => usage.CachedTokens.HasValue)
            ? list.Sum(usage => usage.CachedTokens ?? 0)
            : (int?)null;

        return new UsageInfo
        {
            InputTokens = list.Sum(usage => usage.InputTokens),
            OutputTokens = list.Sum(usage => usage.OutputTokens),
            TotalTokens = list.Sum(usage => usage.TotalTokens),
            CachedTokens = cached,
        };
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png",
        };

    private static string Trim(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    private sealed class SubResult
    {
        public bool Success { get; init; }
        public GeneratedImageData? Image { get; init; }
        public UsageInfo? Usage { get; init; }
        public string? ErrorMessage { get; init; }
        public double ServerTimeSeconds { get; init; }

        public static SubResult Fail(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage,
        };
    }
}
