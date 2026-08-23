using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Kivoy.Services;

public static class CookieVault
{
    private static readonly byte[] Entropy = "Kivoy.CookieVault.v1"u8.ToArray();

    public static string StorePath => Path.Combine(SettingsStore.DataFolder, "youtube_cookies.bin");
    public static string TempPath => Path.Combine(SettingsStore.DataFolder, "youtube_cookies.ytdlp.tmp");
    public static string LegacyPath => Path.Combine(SettingsStore.DataFolder, "youtube_cookies.txt");

    public static bool Exists => File.Exists(StorePath);

    public static void Save(string netscapeText)
    {
        Directory.CreateDirectory(SettingsStore.DataFolder);
        var blob = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(netscapeText),
            Entropy,
            DataProtectionScope.CurrentUser);
        File.WriteAllBytes(StorePath, blob);
    }

    public static bool TryLoad(out string netscapeText)
    {
        netscapeText = "";
        try
        {
            if (!File.Exists(StorePath))
                return false;
            var plain = ProtectedData.Unprotect(
                File.ReadAllBytes(StorePath),
                Entropy,
                DataProtectionScope.CurrentUser);
            netscapeText = Encoding.UTF8.GetString(plain);
            return plain.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static string? PrepareTempCopy()
    {
        try
        {
            if (!TryLoad(out var text))
                return null;
            Directory.CreateDirectory(SettingsStore.DataFolder);
            File.WriteAllText(TempPath, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            try { File.SetAttributes(TempPath, FileAttributes.Hidden); } catch { }
            return TempPath;
        }
        catch
        {
            return null;
        }
    }

    public static void DeleteTemp()
    {
        try { File.Delete(TempPath); } catch { }
    }

    public static void DeleteAll()
    {
        DeleteTemp();
        try { File.Delete(StorePath); } catch { }
    }

    public static bool IsManagedPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            var full = Path.GetFullPath(path);
            return string.Equals(full, Path.GetFullPath(StorePath), StringComparison.OrdinalIgnoreCase)
                || string.Equals(full, Path.GetFullPath(LegacyPath), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static void MigrateLegacyPlaintext()
    {
        DeleteTemp();

        try
        {
            var cf = SettingsStore.Instance.CookiesFile;
            if (string.IsNullOrWhiteSpace(cf) || !File.Exists(cf))
                return;
            if (!string.Equals(Path.GetFullPath(cf), Path.GetFullPath(LegacyPath), StringComparison.OrdinalIgnoreCase))
                return;

            var text = File.ReadAllText(cf);
            if (!text.Contains("# Netscape HTTP Cookie File", StringComparison.OrdinalIgnoreCase))
                return;

            if (!File.Exists(StorePath))
                Save(text);
            SettingsStore.Instance.CookiesFile = StorePath;
            SettingsStore.Save();
            File.Delete(cf);
        }
        catch
        {
            // leave everything untouched on any failure
        }
    }
}
