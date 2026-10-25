using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace RomForge.Core.UI.Helpers;

public static class Win32API
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_STOP = 0;
    private const uint FLASHW_TRAY = 2;

    private static DispatcherTimer? _flashTimer;
    private static IntPtr _flashingHwnd;

    public static void FlashTaskbarIcon(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        if (_flashTimer != null) return;

        _flashingHwnd = hwnd;

        _flashTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(800)
        };

        _flashTimer.Tick += (s, e) =>
        {
            if (window.IsActive)
            {
                StopFlash();
                return;
            }

            FLASHWINFO info = new()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
                hwnd = _flashingHwnd,
                dwFlags = FLASHW_TRAY,
                uCount = 1,
                dwTimeout = 0
            };

            FlashWindowEx(ref info);
        };

        _flashTimer.Start();

        FLASHWINFO initialInfo = new()
        {
            cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
            hwnd = _flashingHwnd,
            dwFlags = FLASHW_TRAY,
            uCount = 1,
            dwTimeout = 0
        };
        FlashWindowEx(ref initialInfo);
    }

    public static void StopFlash()
    {
        _flashTimer?.Stop();
        _flashTimer = null;

        if (_flashingHwnd != IntPtr.Zero)
        {
            FLASHWINFO info = new()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO)),
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