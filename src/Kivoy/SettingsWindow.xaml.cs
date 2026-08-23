using System.Windows;
using Kivoy.Services;
using Kivoy.ViewModels;

namespace Kivoy;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        AppShell.ApplyWindowShell(this, ThemeManager.Current == "Dark");

        var vm = new SettingsViewModel();
        DataContext = vm;
        vm.RequestClose += () => Close();
    }
}
