using System.IO;
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

        // Restore persisted size, but never below the new design minimums.
        Width = Math.Max(SettingsStore.Instance.WindowWidth, MinWidth);
        Height = Math.Max(SettingsStore.Instance.WindowHeight, MinHeight);

        Closing += (_, _) =>
        {
            var s = SettingsStore.Instance;
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            SettingsStore.Save();
        };

        Loaded += (_, _) =>
        {
            UpdateStorageCard();
            if (DataContext is MainViewModel vm)
                vm.PropertyChanged += OnViewModelPropertyChanged;
        };
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.DestinationFolder))
            UpdateStorageCard();
    }

    private void UpdateStorageCard()
    {
        try
        {
            if (DataContext is not MainViewModel vm || string.IsNullOrWhiteSpace(vm.DestinationFolder))
            {
                StorageCard.Visibility = Visibility.Collapsed;
                return;
            }

            var root = Path.GetPathRoot(Path.GetFullPath(vm.DestinationFolder));
            if (string.IsNullOrEmpty(root))
            {
                StorageCard.Visibility = Visibility.Collapsed;
                return;
            }

            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.TotalSize == 0)
            {
                StorageCard.Visibility = Visibility.Collapsed;
                return;
            }

            var free = drive.AvailableFreeSpace;
            var total = drive.TotalSize;
            var usedPercent = (total - free) * 100.0 / total;

            double FormatGb(long bytes) => bytes / 1024d / 1024d / 1024d;

            StorageFreeText.Text =
                $"{FormatGb(free):0.#} GB free of {FormatGb(total):0} GB";
            StorageBar.Value = Math.Clamp(usedPercent, 0, 100);
            StorageCard.Visibility = Visibility.Visible;
        }
        catch
        {
            StorageCard.Visibility = Visibility.Collapsed;
        }
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
