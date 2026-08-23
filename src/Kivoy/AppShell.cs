using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kivoy;

public static class AppShell
{
    private static ImageSource? _icon;
    private static IntPtr _hIconSmall = IntPtr.Zero;
    private static IntPtr _hIconBig = IntPtr.Zero;

    private const uint WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x0010;

    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public static void ApplyWindowShell(Window window, bool isDark)
    {
        ApplyWindowIcon(window);
        ApplyTitleBarTheme(window, isDark);
    }

    public static void ApplyWindowIcon(Window window)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(path))
            {
                if (_icon is null)
                {
                    _icon = BitmapFrame.Create(new Uri(path), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                }

                window.Icon = _icon;

                ApplyNativeWindowIcon(window, path);
            }
        }
        catch
        {
            // icon is cosmetic
        }
    }

    private static void ApplyNativeWindowIcon(Window window, string iconPath)
    {
        void SetNativeIcon(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;

            if (_hIconSmall == IntPtr.Zero)
            {
                int smallX = SystemParameters.SmallIconWidth > 0 ? (int)SystemParameters.SmallIconWidth : 16;
                int smallY = SystemParameters.SmallIconHeight > 0 ? (int)SystemParameters.SmallIconHeight : 16;
                _hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, smallX, smallY, LR_LOADFROMFILE);
            }

            if (_hIconBig == IntPtr.Zero)
            {
                int iconX = SystemParameters.IconWidth > 0 ? (int)SystemParameters.IconWidth : 32;
                int iconY = SystemParameters.IconHeight > 0 ? (int)SystemParameters.IconHeight : 32;
                _hIconBig = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, iconX, iconY, LR_LOADFROMFILE);
            }

            if (_hIconSmall != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _hIconSmall);
            }

            if (_hIconBig != IntPtr.Zero)
            {
                SendMessage(hwnd, WM_SETICON, (IntPtr)ICON_BIG, _hIconBig);
            }
        }

        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero)
        {
            window.SourceInitialized += (_, _) => SetNativeIcon(helper.Handle);
        }
        else
        {
            SetNativeIcon(helper.Handle);
        }
    }

    public static void ApplyTitleBarTheme(Window window, bool isDark)
    {
        try
        {
            var helper = new WindowInteropHelper(window);
            if (helper.Handle == IntPtr.Zero)
            {
                window.SourceInitialized += (_, _) => SetTitleBarDark(helper.Handle, isDark);
            }
            else
            {
                SetTitleBarDark(helper.Handle, isDark);
            }
        }
        catch
        {
            // cosmetic
        }
    }

    private static void SetTitleBarDark(IntPtr hwnd, bool isDark)
    {
        if (hwnd == IntPtr.Zero) return;
        int dark = isDark ? 1 : 0;
        // 20 = DWMWA_USE_IMMERSIVE_DARK_MODE (Win 11 / Win 10 2004+), 19 = older Win 10
        if (DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)) != 0)
        {
            DwmSetWindowAttribute(hwnd, 19, ref dark, sizeof(int));
        }
    }
}

