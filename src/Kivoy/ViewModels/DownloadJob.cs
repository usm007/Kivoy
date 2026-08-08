using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Kivoy.Models;
using Kivoy.Services;

namespace Kivoy.ViewModels;

public partial class DownloadJob : ObservableObject
{
    private readonly DownloadManager _manager;
    private Process? _proc;
    private CancellationTokenSource? _cts;

    public DownloadJob(MediaItem item, DownloadOptions options, DownloadManager manager, string folderPath)
    {
        Item = item;
        Options = options;
        FolderPath = folderPath;
        _manager = manager;

        PauseCommand = new RelayCommand(() => _manager.PauseJob(this), () => CanPause);
        ResumeCommand = new RelayCommand(() => _manager.ResumeJob(this), () => CanResume);
        CancelCommand = new RelayCommand(() => _manager.CancelJob(this), () => CanCancel);
        RetryCommand = new RelayCommand(() => _manager.RetryJob(this), () => CanRetry);
        RemoveCommand = new RelayCommand(() => _manager.RemoveJob(this), () => IsDone);
        OpenFolderCommand = new RelayCommand(() => PathUtil.OpenInExplorer(OutputPath ?? FolderPath), () => OutputPath != null || Directory.Exists(FolderPath));
        PlayCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrWhiteSpace(OutputPath))
                Process.Start(new ProcessStartInfo(OutputPath) { UseShellExecute = true });
        }, () => OutputExists);

        _ = ThumbnailLoader.GetAsync(item.ThumbnailUrl)
            .ContinueWith(t => Thumbnail = t.Result, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public MediaItem Item { get; }
    public DownloadOptions Options { get; }
    public string FolderPath { get; }
    public string Title => Item.Title;

    public RelayCommand PauseCommand { get; }
    public RelayCommand ResumeCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand RetryCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand PlayCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsProgressVisible), nameof(CanPause), nameof(CanResume), nameof(CanCancel), nameof(CanRetry), nameof(IsDone), nameof(StateText))]
    private JobState state = JobState.Queued;

    public string StateText => State switch
    {
        JobState.Queued => "Queued",
        JobState.Downloading => "Downloading",
        JobState.Paused => "Paused",
        JobState.Processing => "Processing",
        JobState.Completed => "Completed",
        JobState.Error => "Failed",
        JobState.Cancelled => "Cancelled",
        _ => ""
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(IsProgressVisible))]
    private double percent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string speedText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string etaText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string sizeText = "";

    [ObservableProperty]
    private double totalBytes;

    public bool TotalBytesKnown => TotalBytes > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private string? errorText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputExists))]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand), nameof(PlayCommand))]
    private string? outputPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProgressVisible))]
    private bool isIndeterminate;

    [ObservableProperty]
    private ImageSource? thumbnail;

    public bool OutputExists => !string.IsNullOrWhiteSpace(OutputPath) && File.Exists(OutputPath);

    public bool IsProgressVisible => State is JobState.Downloading or JobState.Paused or JobState.Processing;
    public bool CanPause => State == JobState.Downloading;
    public bool CanResume => State == JobState.Paused;
    public bool CanCancel => State is JobState.Queued or JobState.Downloading or JobState.Paused or JobState.Processing;
    public bool CanRetry => State == JobState.Error;
    public bool IsDone => State is JobState.Completed or JobState.Cancelled or JobState.Error;

    public string StatusText => State switch
    {
        JobState.Queued => "Waiting…",
        JobState.Downloading when Percent >= 100 => "Finalizing…",
        JobState.Downloading => $"{Percent:0.#}% · {SpeedText} · {EtaText}",
        JobState.Processing => "Processing (merging / converting)…",
        JobState.Paused => $"Paused · {Percent:0.#}%",
        JobState.Completed => "Completed",
        JobState.Cancelled => "Cancelled",
        JobState.Error => Truncate(ErrorText ?? "Download failed", 110),
        _ => ""
    };

    private static string Truncate(string s, int max)
    {
        s = s.Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    public void AttachRun(CancellationTokenSource cts) => _cts = cts;

    public void DetachRun() => _cts = null;

    public Process? Proc
    {
        get => _proc;
        set => _proc = value;
    }

    public CancellationTokenSource? Cts => _cts;

    public void HandleLine(string line, IProgress<JobProgress> progress)
    {
        if (line.StartsWith("__DL__", StringComparison.Ordinal))
        {
            var parts = line.Length > 6 ? line[6..].Split('|') : Array.Empty<string>();
            double GetNum(int i) =>
                i < parts.Length && double.TryParse(parts[i], NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

            var downloaded = GetNum(0);
            var total = GetNum(1);
            var speed = GetNum(2);
            var eta = GetNum(3);

            progress.Report(new JobProgress(
                downloaded,
                total,
                PathUtil.FormatSpeed(speed),
                "ETA " + PathUtil.FormatEta(eta),
                false,
                false));
        }
        else if (line.StartsWith("__PP__", StringComparison.Ordinal))
        {
            progress.Report(new JobProgress(0, 0, "", "", true, false));
        }
        else if (line.Contains("has already been downloaded", StringComparison.OrdinalIgnoreCase))
        {
            progress.Report(new JobProgress(0, 1, "", "", false, true));
        }
    }

    public void ApplyProgress(JobProgress p)
    {
        if (p.Processing)
        {
            State = JobState.Processing;
            return;
        }

        if (p.AlreadyExists)
        {
            Percent = 100;
            SizeText = PathUtil.FormatBytes(p.Downloaded);
            return;
        }

        if (p.Total > 0)
        {
            TotalBytes = p.Total;
            Percent = Math.Clamp(p.Downloaded / p.Total * 100.0, 0, 100);
            SizeText = $"{PathUtil.FormatBytes(p.Downloaded)} / {PathUtil.FormatBytes(p.Total)}";
        }
        else
        {
            SizeText = PathUtil.FormatBytes(p.Downloaded);
        }

        SpeedText = p.SpeedText;
        EtaText = p.EtaText;
        State = JobState.Downloading;
    }
}
