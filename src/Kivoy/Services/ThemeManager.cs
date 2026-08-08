using System.Windows;

namespace Kivoy.Services;

public static class ThemeManager
{
    public static string Current { get; private set; } = "Dark";

    public static void Apply(string theme)
    {
        var actual = theme switch
        {
            "Light" => "Light",
            "Dark" => "Dark",
            _ => SystemIsLight() ? "Light" : "Dark"
        };

        var resources = Application.Current?.Resources;
        if (resources is null)
            return;

        var dicts = resources.MergedDictionaries;
        var old = dicts.FirstOrDefault(d =>
            d.Source is not null && d.Source.OriginalString.StartsWith("Styles/Colors.", StringComparison.OrdinalIgnoreCase));

        if (old is not null)
            dicts.Remove(old);

        dicts.Insert(0, new ResourceDictionary
        {
            Source = new Uri($"Styles/Colors.{actual}.xaml", UriKind.Relative)
        });

        Current = actual;
    }

    private static bool SystemIsLight()
    {
        try
        {
            var key = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
            var value = Microsoft.Win32.Registry.GetValue(key, "AppsUseLightTheme", "1");
            return Convert.ToInt32(value) != 0;
        }
        catch
        {
            return false;
        }
    }
}
