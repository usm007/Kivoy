namespace TubeDrop.Models;

public sealed class QualityOption
{
    public required string Label { get; init; }
    public required string FormatArg { get; init; }
    public long? EstimatedBytes { get; init; }

    public override string ToString() => Label;
}

public sealed class DownloadOptions
{
    public required DownloadMode Mode { get; init; }
    public required QualityOption Quality { get; init; }
    public string? Container { get; init; }
    public string? AudioFormat { get; init; }
    public bool IncludeSubtitles { get; init; }
    public int Connections { get; init; } = 8;
    public required string DestinationFolder { get; init; }
    public string? PlaylistFolder { get; init; }
}

public sealed record JobProgress(
    double Downloaded,
    double Total,
    string SpeedText,
    string EtaText,
    bool Processing,
    bool AlreadyExists);

public sealed record EngineProgress(string Stage, double Percent);

public sealed class ResolvedQuery
{
    public required List<MediaItem> Items { get; init; }
    public bool IsPlaylist { get; init; }
    public string? PlaylistTitle { get; init; }
    public List<QualityOption>? QualityOptions { get; init; }
}
