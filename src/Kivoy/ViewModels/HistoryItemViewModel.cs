using System.Windows.Media;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kivoy.Models;
using Kivoy.Services;

namespace Kivoy.ViewModels;

public partial class HistoryItemViewModel : ObservableObject
{
    private readonly DownloadManager _manager;

    public HistoryItemViewModel(HistoryEntry entry, DownloadManager manager)
    {
        Entry = entry;
        _manager = manager;

        OpenFolderCommand = new RelayCommand(() => PathUtil.OpenInExplorer(Entry.FilePath));
        PlayCommand = new RelayCommand(() =>
        {
            if (HasFile)
                Process.Start(new ProcessStartInfo(Entry.FilePath) { UseShellExecute = true });
        }, () => HasFile);
        CopyLinkCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(Entry.Url))
                Clipboard.SetText(Entry.Url);
        }, () => !string.IsNullOrWhiteSpace(Entry.Url));
        RedownloadCommand = new RelayCommand(Redownload);
        RemoveCommand = new RelayCommand(() =>
        {
            _manager.History.Remove(Entry);
            HistoryStore.Save(_manager.History);
            Removed?.Invoke(Entry);
        });

        _ = ThumbnailLoader.GetAsync(entry.ThumbnailUrl)
            .ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion)
                    Thumbnail = t.Result;
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public Action<HistoryEntry>? Removed { get; set; }

    public HistoryEntry Entry { get; }

    public string Title => Entry.Title;

    public bool HasFile => File.Exists(Entry.FilePath);

    public string MetaText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Entry.QualityText))
                parts.Add(Entry.ModeText == "Audio" ? Entry.QualityText.ToUpperInvariant() : Entry.QualityText);
            if (Entry.SizeBytes > 0)
                parts.Add(PathUtil.FormatBytes(Entry.SizeBytes));
            parts.Add(Entry.CompletedAt.ToString("MMM d, yyyy · HH:mm"));
            return string.Join(" · ", parts);
        }
    }

    [ObservableProperty]
    private ImageSource? thumbnail;

    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand PlayCommand { get; }
    public RelayCommand CopyLinkCommand { get; }
    public RelayCommand RedownloadCommand { get; }
    public RelayCommand RemoveCommand { get; }

    private void Redownload()
    {
        var entry = Entry;

        var item = new MediaItem
        {
            Id = entry.Id,
            Title = entry.Title,
            Channel = entry.Channel,
            Url = entry.Url,
            ThumbnailUrl = entry.ThumbnailUrl,
            Duration = entry.DurationSeconds > 0 ? TimeSpan.FromSeconds(entry.DurationSeconds) : null,
            Index = 1
        };

        var isAudio = entry.ModeText == "Audio";

        var options = new DownloadOptions
        {
            Mode = isAudio ? DownloadMode.Audio : DownloadMode.Video,
            Quality = isAudio
                ? new QualityOption { Label = "Audio only", FormatArg = "bestaudio/best" }
                : MediaResolver.QualityFromLabel(entry.QualityText),
            Container = isAudio ? null : "MP4",
            AudioFormat = isAudio ? entry.AudioFormat : null,
            IncludeSubtitles = entry.IncludeSubtitles,
            Connections = SettingsStore.Instance.Connections,
            DestinationFolder = string.IsNullOrWhiteSpace(entry.PlaylistFolder)
                ? SettingsStore.Instance.OutputFolder
                : Path.Combine(SettingsStore.Instance.OutputFolder, entry.PlaylistFolder),
            PlaylistFolder = null
        };

        _manager.Enqueue(new[] { (item, (string?)null) }, options);
    }
}
