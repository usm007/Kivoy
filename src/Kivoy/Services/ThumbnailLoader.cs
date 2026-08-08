using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kivoy.Services;

public static class ThumbnailLoader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new();
    private static readonly SemaphoreSlim Gate = new(4);

    public static async Task<ImageSource?> GetAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (Cache.TryGetValue(url, out var cached))
            return cached;

        await Gate.WaitAsync(ct);
        try
        {
            if (Cache.TryGetValue(url, out cached))
                return cached;

            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var mem = new MemoryStream();
            await stream.CopyToAsync(mem, ct);
            mem.Position = 0;

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = mem;
            bmp.EndInit();
            bmp.Freeze();

            Cache[url] = bmp;
            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            Gate.Release();
        }
    }
}
