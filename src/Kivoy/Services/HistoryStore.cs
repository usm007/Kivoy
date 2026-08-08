using System.IO;
using System.Text.Json;
using Kivoy.Models;

namespace Kivoy.Services;

public static class HistoryStore
{
    private static readonly string HistoryPath = Path.Combine(SettingsStore.DataFolder, "history.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static List<HistoryEntry> Load()
    {
        try
        {
            if (File.Exists(HistoryPath))
                return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(HistoryPath)) ?? new List<HistoryEntry>();
        }
        catch
        {
            // ignore
        }
        return new List<HistoryEntry>();
    }

    public static void Save(List<HistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.DataFolder);
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(entries, Options));
        }
        catch
        {
            // ignore
        }
    }
}
