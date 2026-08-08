using CommunityToolkit.Mvvm.ComponentModel;

namespace Kivoy.Models;

public partial class MediaItem : ObservableObject
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Channel { get; init; } = "";
    public string Url { get; init; } = "";
    public string ThumbnailUrl { get; init; } = "";
    public TimeSpan? Duration { get; init; }
    public int Index { get; init; }

    [ObservableProperty]
    private bool isSelected = true;

    public string DurationText =>
        Duration is { } d
            ? d.Hours > 0
                ? $"{(int)d.TotalHours}:{d.Minutes:00}:{d.Seconds:00}"
                : $"{d.Minutes}:{d.Seconds:00}"
            : "";
}
