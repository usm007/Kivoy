using System.Windows;
using System.Windows.Threading;
using TubeDrop.Services;

namespace TubeDrop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var log = System.IO.Path.Combine(AppContext.BaseDirectory, "error.log");
                System.IO.File.AppendAllText(log, $"[{DateTime.Now:O}] {args.Exception}\n\n");
                MessageBox.Show(
                    "Something went wrong:\n\n" + args.Exception.Message,
                    "TubeDrop",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            catch
            {
                // ignore
            }
            args.Handled = true;
        };

        SettingsStore.Load();
        ThemeManager.Apply(SettingsStore.Instance.Theme);
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnSystemThemeChanged;

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void OnSystemThemeChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category != Microsoft.Win32.UserPreferenceCategory.General)
            return;
        if (SettingsStore.Instance.Theme != "System")
            return;

        Dispatcher.BeginInvoke(DispatcherPriority.Background, () => ThemeManager.Apply("System"));
    }
}
