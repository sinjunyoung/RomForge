using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using static Common.Win32API;

namespace RomForge.Core.UI.Helpers;

public static partial class FlashTaskbar
{
    private static readonly Lock _lock = new();

    private static DispatcherTimer? _flashTimer;
    private static IntPtr _flashingHwnd;

    public static void FlashTaskbarIcon(Window window)
    {
        IntPtr hwnd = window.Dispatcher.Invoke(() => new WindowInteropHelper(window).Handle);
        if (hwnd == IntPtr.Zero) return;

        lock (_lock)
        {
            if (_flashTimer != null) return;

            _flashingHwnd = hwnd;

            _flashTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };

            _flashTimer.Tick += (s, e) =>
            {
                if (window.Dispatcher.Invoke(() => window.IsActive))
                {
                    StopFlash();
                    return;
                }

                FLASHWINFO info = new()
                {
                    cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd = _flashingHwnd,
                    dwFlags = FLASHW_TRAY,
                    uCount = 1,
                    dwTimeout = 0
                };

                FlashWindowEx(ref info);
            };

            _flashTimer.Start();
        }

        FLASHWINFO initialInfo = new()
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = hwnd,
            dwFlags = FLASHW_TRAY,
            uCount = 1,
            dwTimeout = 0
        };
        FlashWindowEx(ref initialInfo);
    }

    public static void StopFlash()
    {
        lock (_lock)
        {
            _flashTimer?.Stop();
            _flashTimer = null;

            if (_flashingHwnd != IntPtr.Zero)
            {
                FLASHWINFO info = new()
                {
                    cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                    hwnd = _flashingHwnd,
                    dwFlags = FLASHW_STOP,
                    uCount = 0,
                    dwTimeout = 0
                };

                FlashWindowEx(ref info);
                _flashingHwnd = IntPtr.Zero;
            }
        }
    }
}