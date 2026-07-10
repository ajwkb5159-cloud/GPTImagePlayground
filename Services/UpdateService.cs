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
    // GitHub REST API endpoint that lists repository tags.
    private const string TagsApi =
        "https://api.github.com/repos/ajwkb5159-cloud/GPTImagePlayground/tags?per_page=100";

    // Fixed download location: the packaged zip attached to the matching release.
    // {0} is replaced with the tag name (e.g. "1.1.2").
    private const string DownloadUrlTemplate =
        "https://github.com/ajwkb5159-cloud/GPTImagePlayground/releases/download/{0}/GPTImageGenerator.zip";

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
            TagsApi,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var latestTag = FindLatestTag(json);

        var current = CurrentVersion;
        if (latestTag is null)
            return new UpdateCheckResult(false, current, current, null, null);

        var latestVersion = NormalizeVersionText(latestTag);
        var isNewer = CompareVersions(latestVersion, current) > 0;
        var downloadUrl = string.Format(DownloadUrlTemplate, latestTag);
        return new UpdateCheckResult(isNewer, current, latestVersion, downloadUrl, null);
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

    /// <summary>
    /// Scans the tags list and returns the tag name with the highest semantic version.
    /// The Tags API does not guarantee ordering, so every tag is compared explicitly.
    /// </summary>
    private static string? FindLatestTag(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
            return null;

        string? bestTag = null;
        string? bestVersion = null;
        foreach (var tag in root.EnumerateArray())
        {
            if (!tag.TryGetProperty("name", out var nameElement))
                continue;

            var name = nameElement.GetString();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var version = NormalizeVersionText(name);
            if (bestVersion is null || CompareVersions(version, bestVersion) > 0)
            {
                bestVersion = version;
                bestTag = name;
            }
        }

        return bestTag;
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
