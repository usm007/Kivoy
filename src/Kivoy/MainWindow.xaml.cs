using System.Windows;
using System.Windows.Input;
using Kivoy.Services;
using Kivoy.ViewModels;

namespace Kivoy;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
        AppShell.ApplyWindowShell(this, ThemeManager.Current == "Dark");

        Width = SettingsStore.Instance.WindowWidth;
        Height = SettingsStore.Instance.WindowHeight;

        Closing += (_, _) =>
        {
            var s = SettingsStore.Instance;
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            SettingsStore.Save();
        };
    }

    private void NewDownload_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm)
            return;

        var dialog = new AddDownloadWindow { Owner = this, DataContext = vm };
        dialog.ShowDialog();
    }

    private static void Play(DownloadJob job)
    {
        if (job.PlayCommand.CanExecute(null))
            job.PlayCommand.Execute(null);
    }

    private static void Play(HistoryItemViewModel item)
    {
        if (item.PlayCommand.CanExecute(null))
            item.PlayCommand.Execute(null);
    }

    private static T? FindDataContext<T>(DependencyObject? source) where T : class
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: T dc })
                return dc;
            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void DownloadsList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        var job = FindDataContext<DownloadJob>(e.OriginalSource as DependencyObject);
        if (job is not null)
            Play(job);
    }

    private void HistoryList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        var item = FindDataContext<HistoryItemViewModel>(e.OriginalSource as DependencyObject);
        if (item is not null)
            Play(item);
    }
}
