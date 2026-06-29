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
        // Ensure output directory exists
        if (!string.IsNullOrWhiteSpace(_config.OutputDir) && !Directory.Exists(_config.OutputDir))
        {
            Directory.CreateDirectory(_config.OutputDir);
        }

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

        var sw = Stopwatch.StartNew();

        progress?.Report("正在请求 API...");

        HttpResponseMessage response;
        string? requestDebugInfo = null;
        if (imagePaths.Count > 0)
        {
            (response, requestDebugInfo) = await SendEditRequestAsync(httpClient, prompt, imagePaths, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var (resp, reqJson) = await SendGenerationRequestAsync(httpClient, prompt, cancellationToken)
                .ConfigureAwait(false);
            response = resp;
            requestDebugInfo = reqJson;
        }

        var serverTime = sw.Elapsed;

        if (!response.IsSuccessStatusCode)
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
            debugInfo.AppendLine($"{_config.BaseUrl.TrimEnd('/')}/images/generations");

            throw new HttpRequestException(debugInfo.ToString());
        }

        progress?.Report($"API 响应完成（{serverTime.TotalSeconds:F0} 秒），正在解析...");

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

        // Parse images (b64_json first, then URL)
        var images = new List<(byte[] bytes, string mime)>();
        foreach (var item in imageResponse.Data)
        {
            if (!string.IsNullOrWhiteSpace(item.B64Json))
            {
                var bytes = Convert.FromBase64String(item.B64Json);
                images.Add((bytes, "image/png"));
            }
            else if (!string.IsNullOrWhiteSpace(item.Url))
            {
                progress?.Report("正在下载图片...");
                var bytes = await httpClient.GetByteArrayAsync(item.Url, cancellationToken)
                    .ConfigureAwait(false);
                images.Add((bytes, "image/png"));
            }
        }

        if (images.Count == 0)
        {
            throw new InvalidOperationException(
                "接口没有返回可识别的图片数据（既无 b64_json 也无 url）。");
        }

        // Save images
        var savedPaths = new List<string>();
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        for (int i = 0; i < images.Count; i++)
        {
            var outputDir = !string.IsNullOrWhiteSpace(_config.OutputDir)
                ? _config.OutputDir
                : Path.GetTempPath();

            var fileName = Path.Combine(outputDir,
                images.Count == 1
                    ? $"newapi_{timestamp}.png"
                    : $"newapi_{timestamp}_{i + 1}.png");

            await File.WriteAllBytesAsync(fileName, images[i].bytes, cancellationToken)
                .ConfigureAwait(false);
            savedPaths.Add(fileName);
            progress?.Report($"已保存: {fileName} ({images[i].bytes.Length / 1024} KB)");
        }

        sw.Stop();

        return new GenerateResult
        {
            SavedPaths = savedPaths,
            DataUrls = images.Select(img =>
                $"data:{img.mime};base64,{Convert.ToBase64String(img.bytes)}").ToList(),
            Usage = imageResponse.Usage,
            ServerTimeSeconds = serverTime.TotalSeconds,
            TotalTimeSeconds = sw.Elapsed.TotalSeconds,
        };
    }

    private async Task<(HttpResponseMessage response, string requestJson)> SendGenerationRequestAsync(
        HttpClient httpClient,
        string prompt,
        CancellationToken cancellationToken)
    {
        var requestBody = new ImageGenerationRequest
        {
            Model = _config.Model,
            Prompt = prompt,
            N = 1,
            Stream = false,
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
        using var formData = new MultipartFormDataContent();

        formData.Add(new StringContent(_config.Model), "model");
        formData.Add(new StringContent(prompt), "prompt");
        formData.Add(new StringContent("1"), "n");
        formData.Add(new StringContent("false"), "stream");

        var imageNames = new List<string>();
        foreach (var imagePath in imagePaths)
        {
            var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken)
                .ConfigureAwait(false);
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
                GetMimeType(imagePath));
            var fileName = Path.GetFileName(imagePath);
            formData.Add(imageContent, "image", fileName);
            imageNames.Add(fileName);
        }

        var endpoint = $"{_config.BaseUrl.TrimEnd('/')}/images/edits";
        var response = await httpClient.PostAsync(endpoint, formData, cancellationToken)
            .ConfigureAwait(false);

        var debug = $"模型: {_config.Model}\n提示词: {prompt[..Math.Min(prompt.Length, 200)]}\n参考图: {string.Join(", ", imageNames)}\n端点: {endpoint}";
        return (response, debug);
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
}

internal class GenerateResult
{
    public List<string> SavedPaths { get; set; } = [];
    public List<string> DataUrls { get; set; } = [];
    public UsageInfo? Usage { get; set; }
    public double ServerTimeSeconds { get; set; }
    public double TotalTimeSeconds { get; set; }
}
