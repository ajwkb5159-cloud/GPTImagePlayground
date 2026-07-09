using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace ImageGenerator.Services;

/// <summary>
/// Result of querying the latest release from GitHub.
/// </summary>
internal sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string LatestVersion,
    string? DownloadUrl,
    string? ReleaseNotes);

/// <summary>
/// Checks GitHub Releases for a newer build, downloads the packaged zip, and
/// hands off to an external updater script that overwrites the running app and
/// relaunches it. The running executable cannot overwrite itself, so the actual
/// file replacement is performed by a detached batch script after this process exits.
/// </summary>
internal sealed class UpdateService
{
    // GitHub REST API endpoint for the latest published release.
    private const string LatestReleaseApi =
        "https://api.github.com/repos/ajwkb5159-cloud/GPTImagePlayground/releases/latest";

    // Preferred asset name inside the release; any .zip is accepted as a fallback.
    private const string PreferredAssetName = "GPTImageGenerator.zip";

    private readonly bool _verifySslCertificate;

    public UpdateService(bool verifySslCertificate)
    {
        _verifySslCertificate = verifySslCertificate;
    }

    /// <summary>
    /// The version of the currently running assembly, formatted as major.minor.patch.
    /// </summary>
    public static string CurrentVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            return version is null
                ? "1.0.0"
                : $"{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();

        using var response = await client.GetAsync(
            LatestReleaseApi,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var (latestVersion, downloadUrl, notes) = ParseLatestRelease(json);

        var current = CurrentVersion;
        var isNewer = CompareVersions(latestVersion, current) > 0;
        return new UpdateCheckResult(isNewer, current, latestVersion, downloadUrl, notes);
    }

    /// <summary>
    /// Downloads the release zip to a temporary file, reporting progress as a percentage
    /// (or -1 when the total size is unknown).
    /// </summary>
    public async Task<string> DownloadPackageAsync(
        string downloadUrl,
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();

        using var response = await client.GetAsync(
            downloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var tempZipPath = Path.Combine(
            Path.GetTempPath(),
            $"GPTImageGenerator_update_{Guid.NewGuid():N}.zip");

        await using var httpStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            tempZipPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long downloadedBytes = 0;
        int read;
        var lastReportedPercent = -1;
        while ((read = await httpStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloadedBytes += read;

            if (progress is null)
                continue;

            if (totalBytes <= 0)
            {
                progress.Report(-1);
                continue;
            }

            var percent = (int)(downloadedBytes * 100 / totalBytes);
            if (percent != lastReportedPercent)
            {
                lastReportedPercent = percent;
                progress.Report(percent);
            }
        }

        return tempZipPath;
    }

    /// <summary>
    /// Extracts the downloaded package and launches a detached updater script that waits
    /// for this process to exit, overwrites the application files, and relaunches the app.
    /// The caller is responsible for exiting the application right after this returns.
    /// </summary>
    public void LaunchUpdaterAndPrepareExit(string zipPath)
    {
        var appDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var exePath = Environment.ProcessPath ?? Path.Combine(appDir, "ImageGenerator.exe");
        var processId = Environment.ProcessId;

        var extractDir = Path.Combine(
            Path.GetTempPath(),
            $"GPTImageGenerator_extract_{Guid.NewGuid():N}");
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

        var scriptPath = Path.Combine(
            Path.GetTempPath(),
            $"GPTImageGenerator_update_{Guid.NewGuid():N}.bat");
        File.WriteAllText(scriptPath, BuildUpdaterScript(processId, extractDir, appDir, exePath, zipPath), new UTF8Encoding(false));

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            CreateNoWindow = true,
        };
        Process.Start(startInfo);
    }

    private HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler();
        if (!_verifySslCertificate)
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;

        var client = new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromMinutes(10),
        };
        // GitHub's REST API rejects requests without a User-Agent header.
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("GPTImagePlayground", CurrentVersion));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static (string Version, string? DownloadUrl, string? Notes) ParseLatestRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var tag = root.TryGetProperty("tag_name", out var tagElement)
            ? tagElement.GetString() ?? ""
            : "";
        var version = NormalizeVersionText(tag);

        var notes = root.TryGetProperty("body", out var bodyElement)
            ? bodyElement.GetString()
            : null;

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nameElement)
                    ? nameElement.GetString()
                    : null;
                if (name is null || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = asset.TryGetProperty("browser_download_url", out var urlElement)
                    ? urlElement.GetString()
                    : null;
                if (url is null)
                    continue;

                downloadUrl = url;
                if (name.Equals(PreferredAssetName, StringComparison.OrdinalIgnoreCase))
                    break;
            }
        }

        return (version, downloadUrl, notes);
    }

    private static string NormalizeVersionText(string tag)
    {
        var trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V'))
            trimmed = trimmed[1..];
        return trimmed;
    }

    /// <summary>
    /// Compares two dotted version strings numerically. Returns a positive number when
    /// <paramref name="left"/> is newer, negative when older, and zero when equal.
    /// </summary>
    private static int CompareVersions(string left, string right)
    {
        if (Version.TryParse(PadVersion(left), out var leftVersion)
            && Version.TryParse(PadVersion(right), out var rightVersion))
        {
            return leftVersion.CompareTo(rightVersion);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string PadVersion(string version)
    {
        var parts = version.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "0.0",
            1 => $"{parts[0]}.0",
            _ => version,
        };
    }

    private static string BuildUpdaterScript(
        int processId,
        string extractDir,
        string appDir,
        string exePath,
        string zipPath)
    {
        // Waits for the app to exit, copies the extracted files over the install
        // directory, relaunches the app, then cleans up temporary files and itself.
        return $"""
            @echo off
            chcp 65001 >nul
            :waitloop
            tasklist /FI "PID eq {processId}" 2>nul | findstr /I "{processId}" >nul
            if %errorlevel%==0 (
                ping -n 2 127.0.0.1 >nul
                goto waitloop
            )
            xcopy /E /Y /I "{extractDir}\*" "{appDir}\" >nul
            start "" "{exePath}"
            rmdir /S /Q "{extractDir}" >nul 2>&1
            del "{zipPath}" >nul 2>&1
            del "%~f0" >nul 2>&1
            """;
    }
}
