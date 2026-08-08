namespace Kivoy.Services;

public sealed class ToastService
{
    private ToastWindow? _window;

    public void Show(string title, string message)
    {
        if (_window is not null)
        {
            try { _window.Close(); } catch { }
            _window = null;
        }

        _window = new ToastWindow();
        _window.ShowToast(title, message);
    }
}
