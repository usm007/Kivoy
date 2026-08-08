using System.Windows;
using System.Windows.Input;
using Kivoy.Services;
using Kivoy.ViewModels;

namespace Kivoy;

public partial class AddDownloadWindow : Window
{
    public AddDownloadWindow()
    {
        InitializeComponent();
        AppShell.ApplyWindowIcon(this);
        UrlBox.KeyDown += OnUrlBoxKeyDown;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsInDialog = true;
        UrlBox.Focus();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.IsInDialog = false;
    }

    private void OnUrlBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        if (DataContext is MainViewModel vm && vm.AnalyzeCommand.CanExecute(null))
            vm.AnalyzeCommand.Execute(null);
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.DownloadCommand.CanExecute(null))
            vm.DownloadCommand.Execute(null);
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.DismissPanelCommand.Execute(null);
        Close();
    }
}
