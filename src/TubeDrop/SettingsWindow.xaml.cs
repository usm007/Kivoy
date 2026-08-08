using System.Windows;
using TubeDrop.ViewModels;

namespace TubeDrop;

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
