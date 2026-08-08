using System.IO;
using System.Text.Json;
using Kivoy.Models;

namespace Kivoy.Services;

public static class SettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Kivoy",
        "settings.json");

    public static AppSettings Instance { get; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath));
                if (loaded is not null)
                {
                    Instance.OutputFolder = loaded.OutputFolder;
                    Instance.Theme = loaded.Theme;
                    Instance.DefaultMode = loaded.DefaultMode;
                    Instance.DefaultVideoQuality = loaded.DefaultVideoQuality;
                    Instance.DefaultAudioFormat = loaded.DefaultAudioFormat;
                    Instance.DefaultContainer = loaded.DefaultContainer;
                    Instance.MaxConcurrent = Math.Clamp(loaded.MaxConcurrent, 1, 4);
                    Instance.Connections = Math.Clamp(loaded.Connections, 1, 16);
                    Instance.ClipboardDetect = loaded.ClipboardDetect;
                    Instance.NotifyOnComplete = loaded.NotifyOnComplete;
                    Instance.IncludeSubtitles = loaded.IncludeSubtitles;
                    Instance.CookiesFile = loaded.CookiesFile;
                    Instance.Proxy = loaded.Proxy;
                    Instance.WindowWidth = Math.Max(860, loaded.WindowWidth);
                    Instance.WindowHeight = Math.Max(600, loaded.WindowHeight);
                }
            }
        }
        catch
        {
            // corrupted settings -> keep defaults
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Instance, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // ignore write failures
        }
    }

    public static string DataFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kivoy");
}
