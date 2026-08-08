using System.Windows;
using Kivoy.Services;

namespace Kivoy;

public partial class YouTubeSignInWindow : Window
{
    public YouTubeSignInWindow()
    {
        InitializeComponent();
        AppShell.ApplyWindowIcon(this);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await WebView.EnsureCoreWebView2Async();
            var core = WebView.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.DocumentTitleChanged += (_, _) =>
                Title = "Kivoy — " + (string.IsNullOrWhiteSpace(core.DocumentTitle) ? "Sign in with YouTube" : core.DocumentTitle);
            NavigateHome();
        }
        catch (Exception ex)
        {
            StatusText.Text = "WebView2 runtime is unavailable: " + ex.Message;
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("Error");
        }
    }

    private void NavigateHome()
    {
        try
        {
            WebView.CoreWebView2?.Navigate("https://www.youtube.com/");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed to navigate: " + ex.Message;
        }
    }

    private void Reload_Click(object sender, RoutedEventArgs e) => NavigateHome();

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private async void UseSession_Click(object sender, RoutedEventArgs e)
    {
        if (WebView.CoreWebView2 is null)
            return;

        try
        {
            var (path, count, signedIn) = await YouTubeCookieExporter.ExportAsync(WebView.CoreWebView2.CookieManager);
            if (!signedIn)
            {
                StatusText.Text = "You're not signed in yet — sign in with your Google account inside this window first.";
                return;
            }

            SettingsStore.Instance.CookiesFile = path;
            SettingsStore.Save();
            DialogResult = true;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Failed to export cookies: " + ex.Message;
            StatusText.Foreground = (System.Windows.Media.Brush)FindResource("Error");
        }
    }
}
