using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TubeDrop.Models;
using TubeDrop.Services;

namespace TubeDrop.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ToastService _toast;
    private readonly DispatcherTimer _clipboardTimer;
    private string _previousDefaultFolder = "";

    public MainViewModel()
    {
        _toast = new ToastService();
        Manager = new DownloadManager(_toast);

        DestinationFolder = SettingsStore.Instance.OutputFolder;
        _previousDefaultFolder = DestinationFolder;
        Connections = Math.Clamp(SettingsStore.Instance.Connections, 1, 16);

        Items.CollectionChanged += OnItemsChanged;
        Manager.ActiveJobs.CollectionChanged += OnActiveJobsChanged;
        Manager.JobCompleted += OnJobCompleted;

        ActiveJobs = Manager.ActiveJobs;
        RefreshViewJobs();
        UpdateStatusBar();

        foreach (var e in Manager.History)
            _allHistory.Add(e);

        RefreshHistory();

        IsDark = ThemeManager.Current == "Dark";

        _clipboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
        _clipboardTimer.Tick += (_, _) => OnClipboardTick();
        _clipboardTimer.Start();

        InitializeEngineAsync();
    }

    public DownloadManager Manager { get; }
    public ObservableCollection<DownloadJob> ActiveJobs { get; }
    public AppSettings Settings => SettingsStore.Instance;

    public ObservableCollection<MediaItem> Items { get; } = new();
    public ObservableCollection<QualityOption> VideoQualityOptions { get; } = new();
    public ObservableCollection<QualityOption> AudioQualityOptions { get; } = new();
    public ObservableCollection<string> ContainerOptions { get; } = new() { "MP4", "MKV", "WEBM" };
    public ObservableCollection<string> AudioFormats { get; } = new() { "M4A (recommended)", "MP3", "OPUS", "FLAC", "WAV" };
    public ObservableCollection<int> ConnectionsOptions { get; } = new() { 1, 2, 4, 8, 16 };

    private readonly List<HistoryEntry> _allHistory = new();
    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = new();

    // ---------- URL / analyze ----------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private string url = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    private bool isAnalyzing;

    [ObservableProperty]
    private string? error;

    private bool CanAnalyze => Manager is not null && EngineReady && !IsAnalyzing && !string.IsNullOrWhiteSpace(Url);

    [RelayCommand]
    private async Task AnalyzeAsync()
    {
        Error = null;
        var link = Url?.Trim() ?? "";
        if (link.Length == 0)
        {
            Error = "Paste a YouTube link first.";
            return;
        }

        IsAnalyzing = true;
        try
        {
            var resolved = await MediaResolver.ResolveAsync(link, CancellationToken.None);

            Items.Clear();
            foreach (var item in resolved.Items)
                Items.Add(item);

            IsPlaylist = resolved.IsPlaylist;
            PlaylistTitle = resolved.PlaylistTitle;
            PlaylistCount = resolved.Items.Count;
            SourceThumbnail = await ThumbnailLoader.GetAsync(resolved.Items[0].ThumbnailUrl);

            if (resolved.IsPlaylist)
            {
                SourceTitle = null;
                SourceChannel = null;
                SourceDuration = null;
            }
            else
            {
                SourceTitle = resolved.Items[0].Title;
                SourceChannel = resolved.Items[0].Channel;
                SourceDuration = resolved.Items[0].DurationText;
            }

            PopulateQuality(resolved.QualityOptions);
            IsVideoMode = Settings.DefaultMode != DownloadMode.Audio;
            IsAudioMode = Settings.DefaultMode == DownloadMode.Audio;
            IncludeSubtitles = Settings.IncludeSubtitles && IsVideoMode;
            RangeInput = "";
            HasItems = true;
            Error = null;
        }
        catch (YtDlpException ex)
        {
            Items.Clear();
            HasItems = false;
            Error = ex.Message;
        }
        catch (Exception ex)
        {
            Items.Clear();
            HasItems = false;
            Error = ex.Message;
        }
        finally
        {
            IsAnalyzing = false;
        }

        if (HasItems && !IsInDialog)
        {
            var dialog = new AddDownloadWindow { Owner = Application.Current.MainWindow, DataContext = this };
            dialog.ShowDialog();
        }
    }

    public bool IsInDialog { get; set; }

    [RelayCommand]
    private void PasteUrl()
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                var text = Clipboard.GetText().Trim();
                if (text.Length > 0)
                    Url = text;
            }
        }
        catch
        {
            // clipboard busy
        }
    }

    [RelayCommand]
    private void DismissPanel() => HasItems = false;

    // ---------- result panel ----------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private bool hasItems;

    [ObservableProperty]
    private bool isPlaylist;

    [ObservableProperty]
    private string? playlistTitle;

    [ObservableProperty]
    private int playlistCount;

    [ObservableProperty]
    private string? sourceTitle;

    [ObservableProperty]
    private string? sourceChannel;

    [ObservableProperty]
    private string? sourceDuration;

    [ObservableProperty]
    private ImageSource? sourceThumbnail;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private int selectedCount;

    [ObservableProperty]
    private string rangeInput = "";

    // ---------- mode & format ----------

    [ObservableProperty]
    private bool isVideoMode = true;

    [ObservableProperty]
    private bool isAudioMode;

    [ObservableProperty]
    private QualityOption? selectedVideoQuality;

    [ObservableProperty]
    private QualityOption? selectedAudioQuality;

    [ObservableProperty]
    private string selectedContainer = "";

    [ObservableProperty]
    private string selectedAudioFormat = "";

    [ObservableProperty]
    private bool includeSubtitles;

    [ObservableProperty]
    private int connections = 8;

    [ObservableProperty]
    private string destinationFolder = "";

    partial void OnIsVideoModeChanged(bool value)
    {
        if (value)
            IsAudioMode = false;
    }

    partial void OnIsAudioModeChanged(bool value)
    {
        if (value)
            IsVideoMode = false;
    }

    partial void OnSelectedContainerChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            Settings.DefaultContainer = value;
    }

    partial void OnSelectedAudioFormatChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            Settings.DefaultAudioFormat = value;
    }

    private void PopulateQuality(List<QualityOption>? options)
    {
        VideoQualityOptions.Clear();
        foreach (var o in (options ?? MediaResolver.BuildDefaultTiers()))
            VideoQualityOptions.Add(o);

        AudioQualityOptions.Clear();
        AudioQualityOptions.Add(new QualityOption { Label = "Audio only", FormatArg = "bestaudio/best" });

        SelectedVideoQuality = VideoQualityOptions.FirstOrDefault(o =>
            string.Equals(o.Label, Settings.DefaultVideoQuality, StringComparison.OrdinalIgnoreCase))
            ?? VideoQualityOptions.FirstOrDefault();

        SelectedAudioQuality = AudioQualityOptions.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(SelectedContainer) || !ContainerOptions.Contains(SelectedContainer))
            SelectedContainer = ContainerOptions.Contains(Settings.DefaultContainer) ? Settings.DefaultContainer : "MP4";

        if (string.IsNullOrWhiteSpace(SelectedAudioFormat) || !AudioFormats.Contains(SelectedAudioFormat))
            SelectedAudioFormat = AudioFormats.Contains(Settings.DefaultAudioFormat) ? Settings.DefaultAudioFormat : "M4A (recommended)";
    }

    public string DownloadLabel =>
        IsPlaylist ? $"Download {SelectedCount} of {PlaylistCount}" : "Download";

    private bool CanDownload =>
        HasItems && SelectedCount > 0 && EngineReady && !IsAnalyzing && Items.Any(i => i.IsSelected);

    // ---------- playlist selection ----------

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (MediaItem item in e.NewItems)
                item.PropertyChanged += OnItemPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (MediaItem item in e.OldItems)
                item.PropertyChanged -= OnItemPropertyChanged;
        }

        RecomputeSelection();
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaItem.IsSelected))
            RecomputeSelection();
    }

    private void RecomputeSelection()
    {
        SelectedCount = Items.Count(i => i.IsSelected);
        OnPropertyChanged(nameof(DownloadLabel));
        DownloadCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var i in Items)
            i.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var i in Items)
            i.IsSelected = false;
    }

    [RelayCommand]
    private void ApplyRange()
    {
        Error = null;
        var wanted = ParseRange(RangeInput, PlaylistCount);
        if (wanted is null)
        {
            Error = $"Invalid range — use e.g. 1-20, 5, 8-12 (1–{PlaylistCount})";
            return;
        }

        for (var idx = 0; idx < Items.Count; idx++)
            Items[idx].IsSelected = wanted.Contains(idx + 1);
    }

    private static HashSet<int>? ParseRange(string input, int max)
    {
        var set = new HashSet<int>();
        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return null;

        foreach (var part in parts)
        {
            var dash = part.IndexOf('-');
            if (dash > 0 && dash < part.Length - 1)
            {
                if (!int.TryParse(part[..dash], out var a) || !int.TryParse(part[(dash + 1)..], out var b))
                    return null;
                var lo = Math.Min(a, b);
                var hi = Math.Max(a, b);
                if (lo < 1)
                    return null;
                for (var i = lo; i <= hi && i <= max; i++)
                    set.Add(i);
            }
            else if (int.TryParse(part, out var single))
            {
                if (single < 1 || single > max)
                    return null;
                set.Add(single);
            }
            else
            {
                return null;
            }
        }

        return set.Count > 0 ? set : null;
    }

    // ---------- download ----------

    [RelayCommand]
    private void Download()
    {
        if (!CanDownload)
            return;

        var isVideo = IsVideoMode;
        var quality = isVideo ? SelectedVideoQuality : SelectedAudioQuality;
        if (quality is null)
            return;

        var opts = new DownloadOptions
        {
            Mode = isVideo ? DownloadMode.Video : DownloadMode.Audio,
            Quality = quality,
            Container = isVideo ? SelectedContainer : null,
            AudioFormat = isVideo ? null : SelectedAudioFormat,
            IncludeSubtitles = isVideo && IncludeSubtitles,
            Connections = Connections,
            DestinationFolder = DestinationFolder,
            PlaylistFolder = IsPlaylist ? PlaylistTitle : null
        };

        var entries = Items
            .Where(i => i.IsSelected)
            .Select(i => (i, IsPlaylist ? PathUtil.Sanitize(PlaylistTitle ?? "Playlist") : null))
            .ToList();

        Manager.Enqueue(entries, opts);

        SelectedView = SidebarView.Active;
        HasItems = false;
    }

    // ---------- folder ----------

    [RelayCommand]
    private void BrowseDestination()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose download folder",
            InitialDirectory = Directory.Exists(DestinationFolder) ? DestinationFolder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() == true)
        {
            DestinationFolder = dialog.FolderName;
            Settings.OutputFolder = dialog.FolderName;
            SettingsStore.Save();
        }
    }

    [RelayCommand]
    private void OpenDestination() => PathUtil.OpenInExplorer(DestinationFolder);

    // ---------- tabs ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActiveTab), nameof(IsHistoryTab))]
    private int activeTab;

    public bool IsActiveTab
    {
        get => ActiveTab == 0;
        set { if (value) ActiveTab = 0; }
    }

    public bool IsHistoryTab
    {
        get => ActiveTab == 1;
        set { if (value) ActiveTab = 1; }
    }

    // ---------- sidebar views ----------

    public enum SidebarView
    {
        All,
        Active,
        Completed
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsJobsView), nameof(IsCompletedView))]
    private SidebarView selectedView = SidebarView.All;

    public bool IsJobsView => SelectedView != SidebarView.Completed;
    public bool IsCompletedView => SelectedView == SidebarView.Completed;

    public ObservableCollection<DownloadJob> ViewJobs { get; } = new();

    private void OnActiveJobsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
            foreach (DownloadJob j in e.OldItems)
                j.PropertyChanged -= OnJobPropertyChanged;
        if (e.NewItems is not null)
            foreach (DownloadJob j in e.NewItems)
                j.PropertyChanged += OnJobPropertyChanged;

        RecomputeActive();
        RefreshViewJobs();
    }

    private void OnJobPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DownloadJob.State))
        {
            RecomputeActive();
            RefreshViewJobs();
        }
    }

    partial void OnSelectedViewChanged(SidebarView value)
    {
        RefreshViewJobs();
        UpdateStatusBar();
    }

    private void RefreshViewJobs()
    {
        ViewJobs.Clear();
        foreach (var job in ActiveJobs)
        {
            if (SelectedView == SidebarView.Active &&
                job.State is not (JobState.Queued or JobState.Downloading or JobState.Paused or JobState.Processing))
                continue;
            ViewJobs.Add(job);
        }
    }

    [ObservableProperty]
    private string statusBarText = "Ready";

    private void UpdateStatusBar()
    {
        var part = SelectedView switch
        {
            SidebarView.All => $"{ActiveJobs.Count} download(s)",
            SidebarView.Active => $"{ViewJobs.Count} active download(s)",
            _ => $"{HistoryItems.Count} item(s) in history"
        };
        if (Manager.IsPausedAll)
            part += " · paused";
        StatusBarText = part;
    }

    [ObservableProperty]
    private int activeCount;

    [ObservableProperty]
    private bool hasActive;

    [ObservableProperty]
    private int historyCount;

    [ObservableProperty]
    private bool hasHistory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHistory))]
    private string historySearch = "";

    partial void OnHistorySearchChanged(string value) => RefreshHistory();

    private void RecomputeActive()
    {
        ActiveCount = ActiveJobs.Count(j => !j.IsDone);
        HasActive = ActiveJobs.Count > 0;
        UpdateStatusBar();
        OnPropertyChanged(nameof(IsPausedAll));
    }

    public bool IsPausedAll => Manager.IsPausedAll;

    [RelayCommand]
    private void TogglePauseAll()
    {
        if (Manager.IsPausedAll)
            Manager.ResumeAll();
        else
            Manager.PauseAll();
        OnPropertyChanged(nameof(IsPausedAll));
        UpdateStatusBar();
    }

    [RelayCommand]
    private void PauseAll()
    {
        Manager.PauseAll();
        OnPropertyChanged(nameof(IsPausedAll));
        UpdateStatusBar();
    }

    [RelayCommand]
    private void ResumeAll()
    {
        Manager.ResumeAll();
        OnPropertyChanged(nameof(IsPausedAll));
        UpdateStatusBar();
    }

    [RelayCommand]
    private void Exit() => Application.Current.Shutdown();

    [RelayCommand]
    private void About()
    {
        MessageBox.Show(
            $"TubeDrop {typeof(MainViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0"}\n\n" +
            "A clean YouTube video & playlist downloader.\nEngine: yt-dlp + ffmpeg.",
            "About TubeDrop",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ClearFinished() => Manager.ClearFinished();

    // ---------- history ----------

    private void OnJobCompleted(object? sender, DownloadJob job)
    {
        RefreshHistory();
        if (!Settings.NotifyOnComplete)
            return;

        var path = job.OutputPath ?? job.FolderPath;
        _toast.Show(
            "Download complete",
            string.IsNullOrEmpty(path) ? job.Item.Title : $"{job.Item.Title}\n{Path.GetDirectoryName(path)}");
    }

    private void RefreshHistory()
    {
        var search = HistorySearch?.Trim().ToLowerInvariant() ?? "";

        HistoryItems.Clear();
        foreach (var entry in _allHistory)
        {
            if (search.Length > 0 &&
                !entry.Title.ToLowerInvariant().Contains(search) &&
                !entry.Channel.ToLowerInvariant().Contains(search) &&
                !entry.QualityText.ToLowerInvariant().Contains(search))
                continue;

            HistoryItems.Add(new HistoryItemViewModel(entry, Manager)
            {
                Removed = _ => RefreshHistory()
            });
        }

        HistoryCount = _allHistory.Count;
        HasHistory = HistoryItems.Count > 0;
        UpdateStatusBar();
    }

    [RelayCommand]
    private void ClearHistory()
    {
        if (HistoryItems.Count == 0 && _allHistory.Count == 0)
            return;

        _allHistory.Clear();
        HistoryStore.Save(Manager.History);
        RefreshHistory();
    }

    // ---------- engine ----------

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyzeCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommand))]
    private bool engineReady;

    [ObservableProperty]
    private bool engineBusy;

    [ObservableProperty]
    private bool engineFailed;

    [ObservableProperty]
    private string engineStatusText = "Checking engine…";

    [ObservableProperty]
    private double enginePercent;

    [ObservableProperty]
    private string? engineError;

    private async void InitializeEngineAsync() => await EnsureEngineCoreAsync();

    [RelayCommand]
    private async Task RetryEngineAsync()
    {
        EngineFailed = false;
        await EnsureEngineCoreAsync();
    }

    private async Task EnsureEngineCoreAsync()
    {
        EngineBusy = true;
        EnginePercent = 0;
        try
        {
            if (EngineManager.IsReady)
            {
                var version = await EngineManager.GetVersionAsync();
                EngineStatusText = $"Engine ready — yt-dlp {version}";
            }
            else
            {
                var progress = new Progress<EngineProgress>(p =>
                {
                    EngineStatusText = p.Stage;
                    EnginePercent = p.Percent * 100;
                });
                await Task.Run(() => EngineManager.EnsureAsync(progress));
                EngineStatusText = "Engine ready — yt-dlp + ffmpeg";
            }

            EngineReady = true;
        }
        catch (Exception ex)
        {
            EngineFailed = true;
            EngineError = ex.Message;
            EngineStatusText = "Engine setup failed";
        }
        finally
        {
            EngineBusy = false;
        }
    }

    // ---------- app-level ----------

    [ObservableProperty]
    private bool isDark;

    [RelayCommand]
    private void ToggleTheme()
    {
        var next = ThemeManager.Current == "Dark" ? "Light" : "Dark";
        Settings.Theme = next;
        SettingsStore.Save();
        ThemeManager.Apply(next);
        IsDark = ThemeManager.Current == "Dark";
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = new SettingsWindow { Owner = Application.Current.MainWindow };
        window.ShowDialog();

        // follow a default-folder change if the user never overrode it
        if (string.Equals(DestinationFolder, _previousDefaultFolder, StringComparison.OrdinalIgnoreCase))
        {
            DestinationFolder = Settings.OutputFolder;
            _previousDefaultFolder = DestinationFolder;
        }

        Manager.TryStartNextPublic();
        IsDark = ThemeManager.Current == "Dark";
    }

    private void OnClipboardTick()
    {
        if (!Settings.ClipboardDetect || IsAnalyzing || !EngineReady)
            return;

        string? text = null;
        try
        {
            if (Clipboard.ContainsText())
                text = Clipboard.GetText();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text) || !MediaResolver.IsYoutubeUrl(text))
            return;

        var current = Url ?? "";
        if (string.IsNullOrWhiteSpace(current.Trim()) || !MediaResolver.IsYoutubeUrl(current))
            Url = text.Trim();
    }
}
