using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ImageGenerator.Models;

namespace ImageGenerator.Services;

internal class ImageApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly AppConfig _config;

    public ImageApiService(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Generate an image from a text prompt, optionally with reference images.
    /// Returns the saved file path on success, or throws on failure.
    /// </summary>
    public async Task<GenerateResult> GenerateAsync(
        string prompt,
        List<string> imagePaths,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureOutputDirectory();

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            // 兼容自签名/非标准证书
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
        };

        using var httpClient = new HttpClient(handler);
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
        httpClient.Timeout = TimeSpan.FromMinutes(_config.TimeoutMinutes);

        if (_config.UseConcurrentStrategy && _config.ImageCount > 1)
        {
            return await GenerateConcurrentAsync(httpClient, prompt, imagePaths, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        var sw = Stopwatch.StartNew();

        progress?.Report("正在请求 API...");

        HttpResponseMessage response;
        string? requestDebugInfo = null;
        string requestEndpoint;
        if (imagePaths.Count > 0)
        {
            (response, requestDebugInfo) = await SendEditRequestAsync(httpClient, prompt, imagePaths, cancellationToken)
                .ConfigureAwait(false);
            requestEndpoint = $"{_config.BaseUrl.TrimEnd('/')}/images/edits";
        }
        else
        {
            var (resp, reqJson) = await SendGenerationRequestAsync(httpClient, prompt, cancellationToken)
                .ConfigureAwait(false);
            response = resp;
            requestDebugInfo = reqJson;
            requestEndpoint = $"{_config.BaseUrl.TrimEnd('/')}/images/generations";
        }

        using (response)
        {
            var serverTime = sw.Elapsed;

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(await BuildHttpErrorAsync(
                        response,
                        requestDebugInfo,
                        requestEndpoint,
                        cancellationToken)
                    .ConfigureAwait(false));
            }

            progress?.Report($"API 响应完成（{serverTime.TotalSeconds:F0} 秒），正在解析...");

            var images = await ReadImagesFromResponseAsync(httpClient, response, progress, cancellationToken)
                .ConfigureAwait(false);
            var imageResponse = images.Response;

            var savedPaths = await SaveImagesAsync(images.Images, false, progress, cancellationToken)
                .ConfigureAwait(false);

            sw.Stop();

            return new GenerateResult
            {
                SavedPaths = savedPaths,
                DataUrls = images.Images.Select(img =>
                    $"data:{img.Mime};base64,{Convert.ToBase64String(img.Bytes)}").ToList(),
                Usage = imageResponse.Usage,
                ServerTimeSeconds = serverTime.TotalSeconds,
                TotalTimeSeconds = sw.Elapsed.TotalSeconds,
                SuccessCount = savedPaths.Count,
                TotalCount = savedPaths.Count,
            };
        }
    }

    private async Task<GenerateResult> GenerateConcurrentAsync(
        HttpClient httpClient,
        string prompt,
        List<string> imagePaths,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var totalCount = Math.Max(1, _config.ImageCount);
        var maxConcurrency = Clamp(_config.MaxConcurrency, 1, Math.Min(10, totalCount));
        var referenceImages = imagePaths.Count > 0
            ? await LoadReferenceImagesAsync(imagePaths, cancellationToken).ConfigureAwait(false)
            : [];
        var sw = Stopwatch.StartNew();
        using var semaphore = new SemaphoreSlim(maxConcurrency);

        var results = new SubRequestResult[totalCount];
        var tasks = Enumerable.Range(0, totalCount)
            .Select(index => RunSubRequestAsync(index))
            .ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        sw.Stop();

        var succeeded = results
            .Where(result => result?.Success == true && result.ImageBytes != null && result.MimeType != null)
            .Cast<SubRequestResult>()
            .ToList();
        var failedMessages = results
            .Select((result, index) => new { Result = result, Index = index })
            .Where(item => item.Result?.Success != true)
            .Select(item => item.Result?.ErrorMessage ?? $"子请求 {item.Index + 1}/{totalCount}: 未返回结果")
            .ToList();

        if (succeeded.Count == 0)
        {
            throw new InvalidOperationException(
                "所有子请求均失败，未生成可保存的图片。" +
                Environment.NewLine +
                string.Join(Environment.NewLine, failedMessages));
        }

        var images = succeeded
            .Select(result => (Bytes: result.ImageBytes!, Mime: result.MimeType!))
            .ToList();
        var savedPaths = await SaveImagesAsync(images, true, progress, cancellationToken)
            .ConfigureAwait(false);

        return new GenerateResult
        {
            SavedPaths = savedPaths,
            DataUrls = images.Select(img =>
                $"data:{img.Mime};base64,{Convert.ToBase64String(img.Bytes)}").ToList(),
            Usage = AggregateUsage(succeeded.Select(result => result.Usage)),
            ServerTimeSeconds = succeeded.Max(result => result.ServerTimeSeconds),
            TotalTimeSeconds = sw.Elapsed.TotalSeconds,
            SuccessCount = savedPaths.Count,
            TotalCount = totalCount,
            FailedMessages = failedMessages,
        };

        async Task RunSubRequestAsync(int index)
        {
            var acquired = false;
            try
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                acquired = true;

                progress?.Report($"正在请求 API ({index + 1}/{totalCount})...");

                HttpResponseMessage response;
                string? requestDebugInfo;
                string requestEndpoint;
                var requestSw = Stopwatch.StartNew();
                if (referenceImages.Count > 0)
                {
                    (response, requestDebugInfo) = await SendEditRequestWithN1Async(
                            httpClient,
                            prompt,
                            referenceImages,
                            cancellationToken)
                        .ConfigureAwait(false);
                    requestEndpoint = $"{_config.BaseUrl.TrimEnd('/')}/images/edits";
                }
                else
                {
                    var generationResult = await SendGenerationRequestWithN1Async(
                            httpClient,
                            prompt,
                            cancellationToken)
                        .ConfigureAwait(false);
                    response = generationResult.response;
                    requestDebugInfo = generationResult.requestJson;
                    requestEndpoint = $"{_config.BaseUrl.TrimEnd('/')}/images/generations";
                }

                using (response)
                {
                    var serverTime = requestSw.Elapsed;
                    if (!response.IsSuccessStatusCode)
                    {
                        results[index] = SubRequestResult.Fail(await BuildHttpErrorAsync(
                                response,
                                requestDebugInfo,
                                requestEndpoint,
                                cancellationToken)
                            .ConfigureAwait(false));
                        return;
                    }

                    progress?.Report($"API 响应完成 ({index + 1}/{totalCount})，正在解析...");

                    var parsed = await ReadImagesFromResponseAsync(httpClient, response, progress, cancellationToken)
                        .ConfigureAwait(false);
                    var firstImage = parsed.Images.FirstOrDefault();
                    if (firstImage.Bytes == null)
                    {
                        results[index] = SubRequestResult.Fail(
                            $"子请求 {index + 1}/{totalCount}: 接口没有返回可识别的图片数据");
                        return;
                    }

                    results[index] = new SubRequestResult
                    {
                        Success = true,
                        ImageBytes = firstImage.Bytes,
                        MimeType = firstImage.Mime,
                        Usage = parsed.Response.Usage,
                        ServerTimeSeconds = serverTime.TotalSeconds,
                    };
                }
            }
            catch (OperationCanceledException)
            {
                results[index] = SubRequestResult.Fail($"子请求 {index + 1}/{totalCount}: 请求已取消或超时");
            }
            catch (Exception ex)
            {
                results[index] = SubRequestResult.Fail($"子请求 {index + 1}/{totalCount}: {ex.Message}");
            }
            finally
            {
                if (acquired)
                    semaphore.Release();
            }
        }
    }

    private async Task<(HttpResponseMessage response, string requestJson)> SendGenerationRequestAsync(
        HttpClient httpClient,
        string prompt,
        CancellationToken cancellationToken)
    {
        return await SendGenerationRequestCoreAsync(
                httpClient,
                prompt,
                Math.Max(1, _config.ImageCount),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(HttpResponseMessage response, string requestJson)> SendGenerationRequestWithN1Async(
        HttpClient httpClient,
        string prompt,
        CancellationToken cancellationToken)
    {
        return await SendGenerationRequestCoreAsync(httpClient, prompt, 1, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(HttpResponseMessage response, string requestJson)> SendGenerationRequestCoreAsync(
        HttpClient httpClient,
        string prompt,
        int requestCount,
        CancellationToken cancellationToken)
    {
        var requestBody = new ImageGenerationRequest
        {
            Model = _config.Model,
            Prompt = prompt,
            N = requestCount,
            Size = GetResolvedSize(),
            OutputFormat = NormalizeOutputFormat(_config.OutputFormat),
            Stream = false,
            Background = _config.TransparentBackground ? "transparent" : null,
            Moderation = NormalizeModeration(_config.Moderation),
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var endpoint = $"{_config.BaseUrl.TrimEnd('/')}/images/generations";
        var response = await httpClient.PostAsync(endpoint, content, cancellationToken)
            .ConfigureAwait(false);
        return (response, json);
    }

    private async Task<(HttpResponseMessage response, string requestDebug)> SendEditRequestAsync(
        HttpClient httpClient,
        string prompt,
        List<string> imagePaths,
        CancellationToken cancellationToken)
    {
        return await SendEditRequestCoreAsync(
                httpClient,
                prompt,
                imagePaths,
                Math.Max(1, _config.ImageCount),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(HttpResponseMessage response, string requestDebug)> SendEditRequestWithN1Async(
        HttpClient httpClient,
        string prompt,
        List<ReferenceImagePayload> referenceImages,
        CancellationToken cancellationToken)
    {
        return await SendEditRequestCoreAsync(httpClient, prompt, referenceImages, 1, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<ReferenceImagePayload>> LoadReferenceImagesAsync(
        List<string> imagePaths,
        CancellationToken cancellationToken)
    {
        var referenceImages = new List<ReferenceImagePayload>();
        foreach (var imagePath in imagePaths)
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken)
                .ConfigureAwait(false);
            referenceImages.Add(new ReferenceImagePayload(
                Path.GetFileName(imagePath),
                GetMimeType(imagePath),
                imageBytes));
        }

        return referenceImages;
    }

    private async Task<(HttpResponseMessage response, string requestDebug)> SendEditRequestCoreAsync(
        HttpClient httpClient,
        string prompt,
        List<string> imagePaths,
        int requestCount,
        CancellationToken cancellationToken)
    {
        var referenceImages = await LoadReferenceImagesAsync(imagePaths, cancellationToken)
            .ConfigureAwait(false);
        return await SendEditRequestCoreAsync(httpClient, prompt, referenceImages, requestCount, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<(HttpResponseMessage response, string requestDebug)> SendEditRequestCoreAsync(
        HttpClient httpClient,
        string prompt,
        List<ReferenceImagePayload> referenceImages,
        int requestCount,
        CancellationToken cancellationToken)
    {
        using var formData = new MultipartFormDataContent();

        formData.Add(new StringContent(_config.Model), "model");
        formData.Add(new StringContent(prompt), "prompt");
        formData.Add(new StringContent(requestCount.ToString()), "n");
        formData.Add(new StringContent(GetResolvedSize()), "size");
        formData.Add(new StringContent(NormalizeOutputFormat(_config.OutputFormat)), "output_format");
        formData.Add(new StringContent(NormalizeModeration(_config.Moderation)), "moderation");
        formData.Add(new StringContent("false"), "stream");
        if (_config.TransparentBackground)
            formData.Add(new StringContent("transparent"), "background");

        var imageNames = new List<string>();
        foreach (var referenceImage in referenceImages)
        {
            var imageContent = new ByteArrayContent(referenceImage.Bytes);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                referenceImage.MimeType);
            formData.Add(imageContent, "image", referenceImage.FileName);
            imageNames.Add(referenceImage.FileName);
        }

        var endpoint = $"{_config.BaseUrl.TrimEnd('/')}/images/edits";
        var response = await httpClient.PostAsync(endpoint, formData, cancellationToken)
            .ConfigureAwait(false);

        var debug = $"模型: {_config.Model}\n提示词: {prompt[..Math.Min(prompt.Length, 200)]}\n参考图: {string.Join(", ", imageNames)}\nn: {requestCount}\n端点: {endpoint}";
        return (response, debug);
    }

    private async Task<(ImageGenerationResponse Response, List<(byte[] Bytes, string Mime)> Images)> ReadImagesFromResponseAsync(
        HttpClient httpClient,
        HttpResponseMessage response,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var imageResponse = JsonSerializer.Deserialize<ImageGenerationResponse>(responseBody);
        if (imageResponse?.Data is not { Count: > 0 })
        {
            var preview = responseBody.Length > 2000
                ? responseBody[..2000] + "..."
                : responseBody;
            throw new InvalidOperationException(
                $"接口没有返回图片数据。\n── 原始响应 ({responseBody.Length} 字符) ──\n{preview}");
        }

        var images = new List<(byte[] Bytes, string Mime)>();
        var responseMime = GetOutputMime();
        foreach (var item in imageResponse.Data)
        {
            if (!string.IsNullOrWhiteSpace(item.B64Json))
            {
                var bytes = Convert.FromBase64String(item.B64Json);
                images.Add((bytes, responseMime));
            }
            else if (!string.IsNullOrWhiteSpace(item.Url))
            {
                progress?.Report("正在下载图片...");
                var bytes = await httpClient.GetByteArrayAsync(item.Url, cancellationToken)
                    .ConfigureAwait(false);
                images.Add((bytes, responseMime));
            }
        }

        if (images.Count == 0)
        {
            throw new InvalidOperationException(
                "接口没有返回可识别的图片数据（既无 b64_json 也无 url）。");
        }

        return (imageResponse, images);
    }

    private async Task<List<string>> SaveImagesAsync(
        List<(byte[] Bytes, string Mime)> images,
        bool forceIndexedNames,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var savedPaths = new List<string>();
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDir = !string.IsNullOrWhiteSpace(_config.OutputDir)
            ? _config.OutputDir
            : Path.GetTempPath();
        var extension = GetOutputExtension();

        for (var i = 0; i < images.Count; i++)
        {
            var fileName = Path.Combine(outputDir,
                forceIndexedNames || images.Count > 1
                    ? $"newapi_{timestamp}_{i + 1}.{extension}"
                    : $"newapi_{timestamp}.{extension}");

            await File.WriteAllBytesAsync(fileName, images[i].Bytes, cancellationToken)
                .ConfigureAwait(false);
            savedPaths.Add(fileName);
            progress?.Report($"已保存 {fileName} ({images[i].Bytes.Length / 1024} KB)");
        }

        return savedPaths;
    }

    private async Task<string> BuildHttpErrorAsync(
        HttpResponseMessage response,
        string? requestDebugInfo,
        string requestEndpoint,
        CancellationToken cancellationToken)
    {
        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken)
            .ConfigureAwait(false);

        var debugInfo = new StringBuilder();
        debugInfo.AppendLine($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");
        debugInfo.AppendLine();
        debugInfo.AppendLine("── 响应内容 ──");
        debugInfo.AppendLine(errorBody);
        if (requestDebugInfo != null)
        {
            debugInfo.AppendLine();
            debugInfo.AppendLine("── 请求体 ──");
            debugInfo.AppendLine(requestDebugInfo);
        }
        debugInfo.AppendLine();
        debugInfo.AppendLine("── 请求地址 ──");
        debugInfo.AppendLine(requestEndpoint);

        return debugInfo.ToString();
    }

    private void EnsureOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_config.OutputDir) && !Directory.Exists(_config.OutputDir))
            Directory.CreateDirectory(_config.OutputDir);
    }

    private static UsageInfo? AggregateUsage(IEnumerable<UsageInfo?> usages)
    {
        var usageList = usages.Where(usage => usage != null).Cast<UsageInfo>().ToList();
        if (usageList.Count == 0)
            return null;

        var details = usageList
            .Where(usage => usage.InputTokensDetails != null)
            .Select(usage => usage.InputTokensDetails!)
            .ToList();

        return new UsageInfo
        {
            TotalTokens = usageList.Sum(usage => usage.TotalTokens),
            InputTokens = usageList.Sum(usage => usage.InputTokens),
            OutputTokens = usageList.Sum(usage => usage.OutputTokens),
            InputTokensDetails = details.Count == 0
                ? null
                : new TokenDetails
                {
                    TextTokens = details.Sum(detail => detail.TextTokens),
                    ImageTokens = details.Sum(detail => detail.ImageTokens),
                },
        };
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png",
        };
    }

    private string GetResolvedSize() => ImageSizeResolver.Resolve(
        _config.SizeMode,
        _config.SizeTier,
        _config.AspectRatio,
        _config.CustomWidth,
        _config.CustomHeight);

    private static string NormalizeOutputFormat(string? format)
    {
        var value = (format ?? "png").Trim().ToLowerInvariant();
        return value is "png" or "jpeg" or "webp" ? value : "png";
    }

    private static string NormalizeModeration(string? moderation)
    {
        var value = (moderation ?? "auto").Trim().ToLowerInvariant();
        return value is "auto" or "low" ? value : "auto";
    }

    private string GetOutputMime() => NormalizeOutputFormat(_config.OutputFormat) switch
    {
        "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        _ => "image/png",
    };

    private string GetOutputExtension() => NormalizeOutputFormat(_config.OutputFormat) switch
    {
        "jpeg" => "jpg",
        "webp" => "webp",
        _ => "png",
    };

    private static int Clamp(int value, int min, int max) =>
        Math.Min(max, Math.Max(min, value));

    private sealed class SubRequestResult
    {
        public bool Success { get; init; }
        public byte[]? ImageBytes { get; init; }
        public string? MimeType { get; init; }
        public UsageInfo? Usage { get; init; }
        public string? ErrorMessage { get; init; }
        public double ServerTimeSeconds { get; init; }

        public static SubRequestResult Fail(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage,
        };
    }

    private sealed record ReferenceImagePayload(string FileName, string MimeType, byte[] Bytes);
}

internal class GenerateResult
{
    public List<string> SavedPaths { get; set; } = [];
    public List<string> DataUrls { get; set; } = [];
    public UsageInfo? Usage { get; set; }
    public double ServerTimeSeconds { get; set; }
    public double TotalTimeSeconds { get; set; }
    public int SuccessCount { get; set; }
    public int TotalCount { get; set; }
    public List<string> FailedMessages { get; set; } = [];
}
