using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BetterAirPlay;

/// Dresses up the receiver's video window. GStreamer's d3d11videosink opens a plain window titled
/// "Direct3D11 renderer" with a light title bar, and UxPlay has no option to change that. So this watches
/// for that window to appear and, from outside the uxplay process, gives it a black title bar that blends
/// into the video, the app icon, and the connected device's name as its title.
/// Create it on the UI thread: the window-event hook calls back through its message loop.
internal sealed class VideoWindowStyler : IDisposable
{
    const string VideoWindowClass = "GSTD3D11"; // registered by GStreamer's d3d11 window code

    readonly Func<int?> receiverProcessId;
    readonly Icon smallIcon = TrayApp.LoadIcon("app.ico", SystemInformation.SmallIconSize);
    readonly Icon bigIcon = TrayApp.LoadIcon("app.ico", SystemInformation.IconSize);
    readonly Native.WinEventProc onWindowShown; // held so the GC doesn't collect it while native code calls it
    readonly IntPtr hook;
    string title = Program.AppName;

    public VideoWindowStyler(Func<int?> receiverProcessId)
    {
        this.receiverProcessId = receiverProcessId;
        onWindowShown = (_, _, hwnd, idObject, _, _, _) =>
        {
            if (idObject == Native.OBJID_WINDOW && IsVideoWindow(hwnd)) Style(hwnd);
        };
        hook = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, IntPtr.Zero, onWindowShown,
            0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);
    }

    /// Title for the video window; applied to an open window right away.
    public string Title
    {
        get => title;
        set
        {
            title = value;
            Native.EnumWindows((hwnd, _) =>
            {
                if (IsVideoWindow(hwnd)) SetTitle(hwnd);
                return true;
            }, IntPtr.Zero);
        }
    }

    bool IsVideoWindow(IntPtr hwnd)
    {
        if (receiverProcessId() is not { } pid) return false;
        Native.GetWindowThreadProcessId(hwnd, out var owner);
        if (owner != pid) return false;
        var className = new StringBuilder(32);
        Native.GetClassName(hwnd, className, className.Capacity);
        return className.ToString() == VideoWindowClass;
    }

    void Style(IntPtr hwnd)
    {
        // Each attribute fails harmlessly on Windows versions that lack it (caption/text/border colors need Windows 11).
        SetDwmAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
        SetDwmAttribute(hwnd, Native.DWMWA_CAPTION_COLOR, 0x000000);
        SetDwmAttribute(hwnd, Native.DWMWA_TEXT_COLOR, 0xC8C8C8);
        SetDwmAttribute(hwnd, Native.DWMWA_BORDER_COLOR, 0x262626);
        Native.PostMessage(hwnd, Native.WM_SETICON, (IntPtr)Native.ICON_SMALL, smallIcon.Handle);
        Native.PostMessage(hwnd, Native.WM_SETICON, (IntPtr)Native.ICON_BIG, bigIcon.Handle);
        SetTitle(hwnd);
        // Repaint the frame so the dark title bar shows immediately on Windows 10 too.
        Native.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            Native.SWP_FRAMECHANGED | Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
    }

    void SetTitle(IntPtr hwnd) =>
        // A timeout keeps a busy video thread from freezing the tray menu.
        Native.SendMessageTimeout(hwnd, Native.WM_SETTEXT, IntPtr.Zero, title, Native.SMTO_ABORTIFHUNG, 500, out _);

    static void SetDwmAttribute(IntPtr hwnd, int attribute, int value) =>
        Native.DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    public void Dispose()
    {
        Native.UnhookWinEvent(hook);
        smallIcon.Dispose();
        bigIcon.Dispose();
    }

    static class Native
    {
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;
        public const int OBJID_WINDOW = 0;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        public const int DWMWA_BORDER_COLOR = 34;
        public const int DWMWA_CAPTION_COLOR = 35;
        public const int DWMWA_TEXT_COLOR = 36;
        public const uint WM_SETTEXT = 0x000C;
        public const uint WM_SETICON = 0x0080;
        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_FRAMECHANGED = 0x0020;

        public delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module, WinEventProc callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hwnd, StringBuilder className, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeoutMs, out IntPtr result);

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("dwmapi.dll")]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    }
}
