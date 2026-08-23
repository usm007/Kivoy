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
    // Set when the user submits a fresh link; once analysis succeeds the
    // download starts automatically using the existing DownloadCommand.
    private bool _pendingAutoDownload;

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
            AddBarBox.KeyDown += AddBarBox_KeyDown;
        }
    }

    // ---------- Add Download bar flow ----------

    private void AddBarBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SubmitAddBar();
        }
    }

    private void AddBarSubmit_Click(object sender, RoutedEventArgs e) => SubmitAddBar();

    private void SubmitAddBar()
    {
        if (DataContext is not MainViewModel vm)
            return;

        if (!vm.HasItems)
        {
            // Fresh link: analyze first; the watcher auto-starts on success.
            if (vm.AnalyzeCommand.CanExecute(null))
            {
                _pendingAutoDownload = true;
                vm.AnalyzeCommand.Execute(null);
            }
        }
        else
        {
            // Preview already resolved: Enter (re)starts the download.
            TryStartDownload(vm);
        }
    }

    private void TryStartDownload(MainViewModel vm)
    {
        _pendingAutoDownload = false;
        if (vm.DownloadCommand.CanExecute(null))
            vm.DownloadCommand.Execute(null);
    }

    private void AdvancedToggle_Click(object sender, RoutedEventArgs e)
    {
        AdvancedPanel.Visibility = AdvancedPanel.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel vm)
            return;

        if (e.PropertyName != nameof(MainViewModel.HasItems))
            return;

        if (vm.HasItems && _pendingAutoDownload)
        {
            // Analysis succeeded — start the download immediately.
            TryStartDownload(vm);
        }
        else if (!vm.HasItems)
        {
            _pendingAutoDownload = false;

            // Reset the bar for the next link after enqueue or dismiss.
            if (!string.IsNullOrEmpty(AddBarBox.Text))
                vm.Url = string.Empty;
        }
    }

    // ---------- Drag & drop ----------

    private void AddBar_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void AddBar_Drop(object sender, DragEventArgs e)
    {
        var text = e.Data.GetData(DataFormats.UnicodeText) as string
                   ?? e.Data.GetData(DataFormats.Text) as string;

        if (string.IsNullOrWhiteSpace(text) || DataContext is not MainViewModel vm)
            return;

        vm.Url = text.Trim();
        FocusAddBar();
        e.Handled = true;
    }

    // ---------- Global Ctrl+V ----------

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.V ||
            (Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            return;

        // Let TextBoxes handle their own paste.
        if (FocusManager.GetFocusedElement(this) is TextBox)
            return;

        string? text = null;
        try
        {
            if (Clipboard.ContainsText())
                text = Clipboard.GetText();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text) || DataContext is not MainViewModel vm)
            return;

        vm.Url = text.Trim();
        FocusAddBar();
        e.Handled = true;
    }

    private void FocusAddBar()
    {
        AddBarBox.Focus();
        AddBarBox.CaretIndex = AddBarBox.Text.Length;
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
