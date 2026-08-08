using System.IO;
using System.Text.Json.Serialization;

namespace Kivoy.Models;

public sealed class AppSettings
{
    public string OutputFolder { get; set; } = DefaultOutputFolder;
    public string Theme { get; set; } = "System";
    public DownloadMode DefaultMode { get; set; } = DownloadMode.Video;
    public string DefaultVideoQuality { get; set; } = "Best quality";
    public string DefaultAudioFormat { get; set; } = "M4A (recommended)";
    public string DefaultContainer { get; set; } = "MP4";
    public int MaxConcurrent { get; set; } = 3;
    public int Connections { get; set; } = 8;
    public bool ClipboardDetect { get; set; } = true;
    public bool NotifyOnComplete { get; set; } = true;
    public bool IncludeSubtitles { get; set; }
    public string? CookiesFile { get; set; }
    public string? Proxy { get; set; }
    public double WindowWidth { get; set; } = 1180;
    public double WindowHeight { get; set; } = 760;

    [JsonIgnore]
    public static string DefaultOutputFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
}

public sealed class HistoryEntry
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Channel { get; set; } = "";
    public string ThumbnailUrl { get; set; } = "";
    public int DurationSeconds { get; set; }
    public string FilePath { get; set; } = "";
    public long SizeBytes { get; set; }
    public string ModeText { get; set; } = "Video";
    public string QualityText { get; set; } = "";
    public string AudioFormat { get; set; } = "";
    public bool IncludeSubtitles { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.Now;
    public string PlaylistFolder { get; set; } = "";
}
