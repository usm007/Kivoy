using System.Windows;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Kivoy.Models;
using Kivoy.Services;

namespace Kivoy.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public AppSettings S => SettingsStore.Instance;

    public System.Collections.ObjectModel.ObservableCollection<string> Themes { get; } = new() { "System", "Light", "Dark" };
    public System.Collections.ObjectModel.ObservableCollection<string> VideoQualities { get; } =
        new(MediaResolver.Tiers.Select(t => t.Label));
    public System.Collections.ObjectModel.ObservableCollection<string> AudioFormats { get; } =
        new() { "M4A (recommended)", "MP3", "OPUS", "FLAC", "WAV" };
    public System.Collections.ObjectModel.ObservableCollection<string> Containers { get; } =
        new() { "MP4", "MKV", "WEBM" };
    public System.Collections.ObjectModel.ObservableCollection<int> MaxConcurrentOptions { get; } =
        new() { 1, 2, 3, 4 };
    public System.Collections.ObjectModel.ObservableCollection<int> ConnectionsOptions { get; } =
        new() { 1, 2, 4, 8, 16 };

    [ObservableProperty]
    private string outputFolder;

    [ObservableProperty]
    private string theme;

    [ObservableProperty]
    private string defaultVideoQuality;

    [ObservableProperty]
    private string defaultAudioFormat;

    [ObservableProperty]
    private string defaultContainer;

    [ObservableProperty]
    private int maxConcurrent;

    [ObservableProperty]
    private int connections;

    [ObservableProperty]
    private bool clipboardDetect;

    [ObservableProperty]
    private bool notifyOnComplete;

    [ObservableProperty]
    private bool includeSubtitles;

    [ObservableProperty]
    private string? cookiesFile;

    [ObservableProperty]
    private string? proxy;

    [ObservableProperty]
    private string ytDlpVersion = "checking…";

    [ObservableProperty]
    private string? youTubeStatus;

    public bool IsYouTubeSignedIn => File.Exists(YouTubeCookieExporter.CookiePath);

    [ObservableProperty]
    private bool isUpdating;

    [ObservableProperty]
    private string? updateError;

    public SettingsViewModel()
    {
        outputFolder = S.OutputFolder;
        theme = S.Theme;
        defaultVideoQuality = S.DefaultVideoQuality;
        defaultAudioFormat = S.DefaultAudioFormat;
        defaultContainer = S.DefaultContainer;
        maxConcurrent = S.MaxConcurrent;
        connections = S.Connections;
        clipboardDetect = S.ClipboardDetect;
        notifyOnComplete = S.NotifyOnComplete;
        includeSubtitles = S.IncludeSubtitles;
        cookiesFile = S.CookiesFile;
        proxy = S.Proxy;

        LoadVersionAsync();
    }

    public RelayCommand BrowseFolderCommand => new(() =>
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Choose default download folder",
            InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dlg.ShowDialog() == true)
        {
            OutputFolder = dlg.FolderName;
            Save();
        }
    });

    public RelayCommand BrowseCookiesCommand => new(() =>
    {
        var dlg = new OpenFileDialog
        {
            Title = "Choose cookies file",
            Filter = "Cookies file (*.txt)|*.txt|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            CookiesFile = dlg.FileName;
            Save();
        }
    });

    public RelayCommand ClearCookiesCommand => new(() =>
    {
        CookiesFile = null;
        Save();
    });

    public RelayCommand SignInGoogleCommand => new(() =>
    {
        var owner = Application.Current.Windows.OfType<Window>()
            .FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
        var win = new YouTubeSignInWindow { Owner = owner };
        win.ShowDialog();

        OnPropertyChanged(nameof(IsYouTubeSignedIn));
        YouTubeStatus = IsYouTubeSignedIn
            ? "YouTube account linked — cookies exported to the app data folder."
            : null;
    });

    public RelayCommand SignOutGoogleCommand => new(() =>
    {
        YouTubeCookieExporter.SignOut();
        CookiesFile = S.CookiesFile;
        Save();
        OnPropertyChanged(nameof(IsYouTubeSignedIn));
        YouTubeStatus = "Signed out — the saved session was removed.";
    });

    public RelayCommand OpenDataFolderCommand => new(() => PathUtil.OpenInExplorer(SettingsStore.DataFolder));

    public RelayCommand DoneCommand => new(() => CloseWindow());

    public event Action? RequestClose;

    private void CloseWindow() => RequestClose?.Invoke();

    public RelayCommand UpdateYtDlpCommand => new(async () => await UpdateYtDlpAsync());

    private async Task UpdateYtDlpAsync()
    {
        if (IsUpdating)
            return;

        IsUpdating = true;
        UpdateError = null;
        try
        {
            var progress = new Progress<EngineProgress>(p => { });
            await Task.Run(() => EngineManager.UpdateYtDlpAsync(progress));
            YtDlpVersion = "updated — " + await EngineManager.GetVersionAsync();
        }
        catch (Exception ex)
        {
            UpdateError = ex.Message;
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private async void LoadVersionAsync()
    {
        YtDlpVersion = await EngineManager.GetVersionAsync();
    }

    partial void OnOutputFolderChanged(string value) => Save();
    partial void OnThemeChanged(string value)
    {
        S.Theme = value;
        ThemeManager.Apply(value);
        SettingsStore.Save();
    }

    partial void OnDefaultVideoQualityChanged(string value) => Save();
    partial void OnDefaultAudioFormatChanged(string value) => Save();
    partial void OnDefaultContainerChanged(string value) => Save();

    partial void OnMaxConcurrentChanged(int value)
    {
        S.MaxConcurrent = Math.Clamp(value, 1, 4);
        SettingsStore.Save();
    }

    partial void OnConnectionsChanged(int value)
    {
        S.Connections = Math.Clamp(value, 1, 16);
        SettingsStore.Save();
    }

    partial void OnClipboardDetectChanged(bool value) => Save();
    partial void OnNotifyOnCompleteChanged(bool value) => Save();
    partial void OnIncludeSubtitlesChanged(bool value) => Save();
    partial void OnCookiesFileChanged(string? value) => Save();
    partial void OnProxyChanged(string? value) => Save();

    private void Save()
    {
        S.OutputFolder = OutputFolder;
        S.DefaultVideoQuality = DefaultVideoQuality;
        S.DefaultAudioFormat = DefaultAudioFormat;
        S.DefaultContainer = DefaultContainer;
        S.ClipboardDetect = ClipboardDetect;
        S.NotifyOnComplete = NotifyOnComplete;
        S.IncludeSubtitles = IncludeSubtitles;
        S.CookiesFile = CookiesFile;
        S.Proxy = Proxy;
        SettingsStore.Save();
    }
}
