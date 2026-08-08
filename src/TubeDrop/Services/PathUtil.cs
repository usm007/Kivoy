using System.IO;

namespace TubeDrop.Services;

public static class PathUtil
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars()
        .Concat(new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' })
        .Distinct()
        .ToArray();

    public static string Sanitize(string name)
    {
        var cleaned = new string(name.Select(c => InvalidChars.Contains(c) ? ' ' : c).ToArray())
            .Trim()
            .TrimEnd('.', ' ');

        // collapse repeated spaces
        while (cleaned.Contains("  "))
            cleaned = cleaned.Replace("  ", " ");

        return string.IsNullOrWhiteSpace(cleaned) ? "Playlist" : cleaned;
    }

    public static string FormatBytes(double bytes) =>
        bytes switch
        {
            >= 1024 * 1024 * 1024 => $"{bytes / (1024 * 1024 * 1024):0.0} GB",
            >= 1024 * 1024 => $"{bytes / (1024 * 1024):0.0} MB",
            >= 1024 => $"{bytes / 1024:0} KB",
            _ => $"{bytes:0} B"
        };

    public static string FormatSpeed(double bytesPerSecond) =>
        bytesPerSecond <= 0 ? "—" : FormatBytes(bytesPerSecond) + "/s";

    public static string FormatEta(double seconds) =>
        seconds <= 0
            ? "—"
            : seconds < 60
                ? $"{seconds:0}s"
                : seconds < 3600
                    ? $"{seconds / 60:0}m {seconds % 60:0}s"
                    : $"{seconds / 3600:0}h {(seconds % 3600) / 60:0}m";

    public static void OpenInExplorer(string path)
    {
        try
        {
            if (File.Exists(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            else if (Directory.Exists(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }
}
