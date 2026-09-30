using ImageGenerator.Models;

namespace ImageGenerator.Services;

/// <summary>Raw image bytes plus the mime type they were decoded from.</summary>
internal sealed record GeneratedImageData(byte[] Bytes, string MimeType);

/// <summary>
/// Single place that turns decoded images into files on disk (naming, output directory, progress
/// reporting), shared by the OpenAI image path and the Gemini image path.
/// </summary>
internal sealed class GeneratedImageSaver
{
    private readonly AppConfig _config;

    public GeneratedImageSaver(AppConfig config)
    {
        _config = config;
    }

    public string OutputMime => NormalizeOutputFormat(_config.OutputFormat) switch
    {
        "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        _ => "image/png",
    };

    public string OutputExtension => NormalizeOutputFormat(_config.OutputFormat) switch
    {
        "jpeg" => "jpg",
        "webp" => "webp",
        _ => "png",
    };

    public void EnsureOutputDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_config.OutputDir) && !Directory.Exists(_config.OutputDir))
            Directory.CreateDirectory(_config.OutputDir);
    }

    public async Task<List<string>> SaveAsync(
        IReadOnlyList<GeneratedImageData> images,
        bool forceIndexedNames,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var savedPaths = new List<string>();
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var outputDir = !string.IsNullOrWhiteSpace(_config.OutputDir)
            ? _config.OutputDir
            : Path.GetTempPath();

        for (var i = 0; i < images.Count; i++)
        {
            // The extension follows the bytes that actually arrived: the Gemini interface ignores
            // output_format and may hand back JPEG even when PNG was configured.
            var extension = ExtensionFor(images[i].MimeType);
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

    /// <summary>Extension matching the payload, falling back to the configured output format.</summary>
    private string ExtensionFor(string? mimeType) =>
        (mimeType ?? "").Trim().ToLowerInvariant() switch
        {
            "image/jpeg" => "jpg",
            "image/webp" => "webp",
            "image/png" => "png",
            "image/gif" => "gif",
            "image/bmp" => "bmp",
            _ => OutputExtension,
        };

    public static string NormalizeOutputFormat(string? format)
    {
        var value = (format ?? "png").Trim().ToLowerInvariant();
        return value is "png" or "jpeg" or "webp" ? value : "png";
    }
}
