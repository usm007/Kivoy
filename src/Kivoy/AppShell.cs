using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Kivoy;

public static class AppShell
{
    private static BitmapImage? _icon;

    public static void ApplyWindowIcon(Window window)
    {
        try
        {
            if (_icon is null)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
                if (File.Exists(path))
                {
                    var icon = new BitmapImage();
                    icon.BeginInit();
                    icon.UriSource = new Uri(path);
                    icon.CacheOption = BitmapCacheOption.OnLoad;
                    icon.EndInit();
                    icon.Freeze();
                    _icon = icon;
                }
            }

            if (_icon is not null)
                window.Icon = _icon;
        }
        catch
        {
            // icon is cosmetic
        }
    }
}
