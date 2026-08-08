using System.Windows;
using Kivoy.ViewModels;

namespace Kivoy;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        AppShell.ApplyWindowIcon(this);

        var vm = new SettingsViewModel();
        DataContext = vm;
        vm.RequestClose += () => Close();
    }
}
