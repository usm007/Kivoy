using System.IO;
using System.IO.Compression;
using System.Net.Http;
using Kivoy.Models;

namespace Kivoy.Services;

public sealed class EngineMissingException : Exception
{
    public EngineMissingException(string message) : base(message) { }
}

public static class EngineManager
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(8)
    };

    static EngineManager()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Kivoy/1.0 (+https://github.com/usm007/Kivoy)");
    }

    public static string BinDir => Path.Combine(SettingsStore.DataFolder, "bin");

    /// <summary>Folder bundled with the app (Program Files) that can seed the engines on first run.</summary>
    public static string SeedsDir => Path.Combine(AppContext.BaseDirectory, "engines");

    public static string YtDlpPath => FindInPath("yt-dlp.exe") ?? Path.Combine(BinDir, "yt-dlp.exe");
    public static string FfmpegPath => FindInPath("ffmpeg.exe") ?? Path.Combine(BinDir, "ffmpeg.exe");
    public static string FfprobePath => FindInPath("ffprobe.exe") ?? Path.Combine(BinDir, "ffprobe.exe");
    public static string QuickJsPath => FindInPath("qjs.exe") ?? Path.Combine(BinDir, "qjs.exe");
    public static string DenoPath => FindInPath("deno.exe") ?? Path.Combine(BinDir, "deno.exe");

    public static string? FindInPath(string exeName)
    {
        var localBin = Path.Combine(BinDir, exeName);
        if (File.Exists(localBin))
            return localBin;

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var full = Path.Combine(dir.Trim(), exeName);
                if (File.Exists(full))
                    return full;
            }
            catch
            {
                // ignore path parsing errors
            }
        }
        return null;
    }

    public static bool IsReady => File.Exists(YtDlpPath) && File.Exists(FfmpegPath);

    public static async Task EnsureAsync(IProgress<EngineProgress>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(BinDir);

        if (!File.Exists(YtDlpPath) && !TrySeed("yt-dlp.exe"))
            await DownloadYtDlpAsync(progress, 0, 0.12, ct);

        if (!File.Exists(QuickJsPath) && !TrySeed("qjs.exe"))
            await DownloadQuickJsAsync(progress, 0.12, 0.13, ct);

        if (!File.Exists(FfmpegPath))
        {
            if (!TrySeed("ffmpeg.exe") || !TrySeed("ffprobe.exe"))
                await DownloadFfmpegAsync(progress, 0.25, 0.75, ct);
        }
        else if (!File.Exists(FfprobePath) && !TrySeed("ffprobe.exe"))
        {
            await DownloadFfmpegAsync(progress, 0.25, 0.75, ct);
        }

        // sanity check
        var version = await GetVersionAsync(ct);
        progress?.Report(new EngineProgress($"Engine ready — yt-dlp {version} · ffmpeg", 1.0));
    }

    /// <summary>Checks whether a newer yt-dlp is available and updates when one exists.</summary>
    public static async Task<(bool updated, string? newVersion)> CheckForUpdatesAsync(IProgress<EngineProgress>? progress, CancellationToken ct = default)
    {
        var latest = await GetLatestYtDlpVersionAsync(ct);
        if (latest is null)
        {
            progress?.Report(new EngineProgress("Update check failed (offline?)", 1.0));
            return (false, null);
        }

        var current = await GetVersionAsync(ct);
        if (IsVersionNewer(latest, current))
        {
            progress?.Report(new EngineProgress($"Updating yt-dlp to {latest}…", 0));
            await UpdateYtDlpAsync(progress, ct);
            progress?.Report(new EngineProgress($"yt-dlp updated to {latest}", 1.0));
            return (true, latest);
        }
        else
        {
            progress?.Report(new EngineProgress("yt-dlp is up to date", 1.0));
            return (false, latest);
        }
    }

    private static async Task<string?> GetLatestYtDlpVersionAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await Http.GetAsync("https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest", ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);
            const string marker = "\"tag_name\":\"";
            var idx = json.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;
            idx += marker.Length;
            var end = json.IndexOf('"', idx);
            return end > idx ? json[idx..end] : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsVersionNewer(string? latest, string? current)
    {
        static long Key(string? v)
        {
            if (string.IsNullOrWhiteSpace(v))
                return 0;
            var digits = new string(v.Where(char.IsDigit).ToArray());
            return long.TryParse(digits, out var n) ? n : 0;
        }
        return Key(latest) > Key(current);
    }

    private static bool TrySeed(string fileName)
    {
        try
        {
            var src = Path.Combine(SeedsDir, fileName);
            if (!File.Exists(src))
                return false;
            File.Copy(src, Path.Combine(BinDir, fileName), overwrite: true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task UpdateYtDlpAsync(IProgress<EngineProgress>? progress, CancellationToken ct = default)
    {
        Directory.CreateDirectory(BinDir);
        await DownloadYtDlpAsync(progress, 0, 1.0, ct);
        progress?.Report(new EngineProgress("yt-dlp updated", 1.0));
    }

    public static async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        if (!File.Exists(YtDlpPath))
            return "not installed";

        try
        {
            using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            if (proc is null)
                return "not installed";

            var readTask = proc.StandardOutput.ReadToEndAsync();
            var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(15), ct));
            var output = completed == readTask ? await readTask : null;
            try { proc.Kill(true); } catch { }
            return (output ?? "").Trim().Split('\n')[0].Trim();
        }
        catch
        {
            return "not installed";
        }
    }

    private static async Task DownloadYtDlpAsync(
        IProgress<EngineProgress>? progress,
        double start,
        double span,
        CancellationToken ct)
    {
        const string url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        var tmp = YtDlpPath + ".tmp";
        await DownloadToFileAsync(url, tmp, progress, "Downloading yt-dlp…", start, span, ct);
        File.Move(tmp, YtDlpPath, overwrite: true);
    }

    private static async Task DownloadQuickJsAsync(
        IProgress<EngineProgress>? progress,
        double start,
        double span,
        CancellationToken ct)
    {
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
            == System.Runtime.InteropServices.Architecture.X86
                ? "x86"
                : "x86_64";
        var url = $"https://github.com/quickjs-ng/quickjs/releases/latest/download/qjs-windows-{arch}.exe";
        var tmp = QuickJsPath + ".tmp";

        progress?.Report(new EngineProgress("Downloading QuickJS (lightweight JS runtime)…", start));
        await DownloadToFileAsync(url, tmp, progress, "Downloading QuickJS (JS runtime)…", start, start + span, ct);

        File.Move(tmp, QuickJsPath, overwrite: true);
        progress?.Report(new EngineProgress("QuickJS engine ready", start + span));
    }

    private static async Task DownloadFfmpegAsync(
        IProgress<EngineProgress>? progress,
        double start,
        double span,
        CancellationToken ct)
    {
        const string url = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        var tmp = Path.Combine(BinDir, "ffmpeg.zip");
        var stage = start;

        progress?.Report(new EngineProgress("Downloading ffmpeg…", stage));

        await DownloadToFileAsync(url, tmp, progress, "Downloading ffmpeg…", start, start + span * 0.9, ct);

        progress?.Report(new EngineProgress("Extracting ffmpeg…", start + span * 0.92));
        var extractDir = Path.Combine(BinDir, "ffmpeg-extract");
        if (Directory.Exists(extractDir))
            Directory.Delete(extractDir, true);
        Directory.CreateDirectory(extractDir);

        await System.Threading.Tasks.Task.Run(() => ZipFile.ExtractToDirectory(tmp, extractDir), ct);

        var ffmpeg = Directory
            .GetFiles(extractDir, "ffmpeg.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
        var ffprobe = Directory
            .GetFiles(extractDir, "ffprobe.exe", SearchOption.AllDirectories)
            .FirstOrDefault();

        if (ffmpeg is null || ffprobe is null)
            throw new EngineMissingException("Could not find ffmpeg in the downloaded archive.");

        File.Move(ffmpeg, FfmpegPath, overwrite: true);
        File.Move(ffprobe, Path.Combine(BinDir, "ffprobe.exe"), overwrite: true);

        try { File.Delete(tmp); Directory.Delete(extractDir, true); } catch { }
        progress?.Report(new EngineProgress("Extracting ffmpeg…", start + span));
    }

    private static async Task DownloadToFileAsync(
        string url,
        string dest,
        IProgress<EngineProgress>? progress,
        string stage,
        double start,
        double span,
        CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await using var file = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await stream.ReadAsync(buffer, ct)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0)
            {
                var pct = (double)read / total;
                progress?.Report(new EngineProgress(stage, start + span * pct));
            }
        }
    }
}
