using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace TubeDrop;

public partial class ToastWindow : Window
{
    private readonly DispatcherTimer _closeTimer;

    public ToastWindow()
    {
        InitializeComponent();
        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            FadeOut();
        };
    }

    public void ShowToast(string title, string message)
    {
        TitleText.Text = title;
        MessageText.Text = message;

        Show();
        UpdateLayout();

        var work = SystemParameters.WorkArea;
        Left = work.Right - ActualWidth - 18;
        Top = work.Bottom - ActualHeight - 18;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        fadeIn.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        Root.BeginAnimation(OpacityProperty, fadeIn);

        _closeTimer.Start();
    }

    private void FadeOut()
    {
        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(260));
        fadeOut.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn };
        fadeOut.Completed += (_, _) => Close();
        Root.BeginAnimation(OpacityProperty, fadeOut);
    }
}
