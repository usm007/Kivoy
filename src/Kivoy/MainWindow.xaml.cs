using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
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

        // Restore persisted size, but never below the design minimums.
        Width = Math.Max(SettingsStore.Instance.WindowWidth, MinWidth);
        Height = Math.Max(SettingsStore.Instance.WindowHeight, MinHeight);

        Closing += (_, _) =>
        {
            var s = SettingsStore.Instance;
            s.WindowWidth = Width;
            s.WindowHeight = Height;
            SettingsStore.Save();
        };

        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            ComposerUrlBox.KeyDown += ComposerUrlBox_KeyDown;
        }
    }

    // ---------- Add Download composer ----------

    private bool IsComposerOpen => ComposerHost.Visibility == Visibility.Visible;

    private void NewDownload_Click(object sender, RoutedEventArgs e)
    {
        ComposerHost.Visibility = Visibility.Visible;

        if (DataContext is MainViewModel { HasItems: false } vm)
        {
            // fresh session: put focus in the link box
            ComposerUrlBox.Focus();
        }
    }

    private void ComposerCancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.DismissPanelCommand.Execute(null);
        CloseComposer();
    }

    private void ComposerUrlBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not MainViewModel vm)
            return;

        e.Handled = true;
        if (vm.AnalyzeCommand.CanExecute(null))
            vm.AnalyzeCommand.Execute(null);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.HasItems)
            && sender is MainViewModel { HasItems: false }
            && IsComposerOpen)
        {
            // download started (or panel dismissed) — back to the list
            CloseComposer();
        }
    }

    private void CloseComposer()
    {
        ComposerHost.Visibility = Visibility.Collapsed;
    }

    // ---------- List interactions ----------

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
            source = VisualTreeHelper.GetParent(source);
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

    // "⋮" button on cards — opens the row's context menu at the pointer.
    private void CardMore_Click(object sender, RoutedEventArgs e)
    {
        DependencyObject? current = sender as DependencyObject;
        while (current is not null && current is not ListViewItem)
            current = VisualTreeHelper.GetParent(current);

        if (current is ListViewItem lvi && lvi.ContextMenu is { } menu)
        {
            menu.PlacementTarget = lvi;
            menu.Placement = PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
