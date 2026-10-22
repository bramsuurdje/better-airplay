using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace BetterAirPlay;

/// Replaces the frame of the receiver's video window. GStreamer's d3d11videosink opens a plain window titled
/// "Direct3D11 renderer" with a standard title bar in the screen's top-left corner, and UxPlay can't change that.
/// So this watches for that window and, from outside the uxplay process:
/// - removes its title bar (keeping the resize edges on Windows 11) and gives it rounded corners, a dark border and the app icon;
/// - fits it to the screen and centers it;
/// - shows a HoverBar (title, minimize, full screen, close) that slides down when the mouse reaches the top.
/// Create it on the UI thread: the window-event hook and the hover timer run through its message loop.
internal sealed class VideoWindowChrome : IDisposable
{
    const string VideoWindowClass = "GSTD3D11"; // registered by GStreamer's d3d11 window code
    const int RevealZone = 48;                  // logical pixels from the top of the video that bring up the bar
    const double MaxScreenShare = 0.85;         // largest part of the screen a new video window takes up
    static readonly TimeSpan HideDelay = TimeSpan.FromMilliseconds(700);
    // Windows 10 draws a light strip along the top of a resizable window without a title bar, so there the
    // window gets no resize edges (full screen still works). The app manifest makes this report the real version.
    static readonly bool KeepResizeEdges = Environment.OSVersion.Version.Build >= 22000;

    readonly Func<int?> receiverProcessId;
    readonly Icon smallIcon = TrayApp.LoadIcon("app.ico", SystemInformation.SmallIconSize);
    readonly Icon bigIcon = TrayApp.LoadIcon("app.ico", SystemInformation.IconSize);
    readonly Native.WinEventProc onWindowShown; // held so the GC doesn't collect it while native code calls it
    readonly IntPtr hook;
    readonly HoverBar bar = new();
    readonly Timer hoverCheck = new() { Interval = 50 };
    IntPtr video;           // the video window being decorated, or zero
    SavedFrame? fullscreen; // how to restore the window, while the full-screen button has it full screen
    DateTime lastHover;
    string title = Program.AppName;

    sealed class SavedFrame(int style, Native.WINDOWPLACEMENT placement)
    {
        public int Style { get; } = style;
        public Native.WINDOWPLACEMENT Placement = placement;
    }

    public VideoWindowChrome(Func<int?> receiverProcessId)
    {
        this.receiverProcessId = receiverProcessId;
        onWindowShown = (_, _, hwnd, idObject, _, _, _) =>
        {
            if (idObject == Native.OBJID_WINDOW && hwnd != video && IsVideoWindow(hwnd)) Adopt(hwnd);
        };
        hook = Native.SetWinEventHook(Native.EVENT_OBJECT_SHOW, Native.EVENT_OBJECT_SHOW, IntPtr.Zero, onWindowShown,
            0, 0, Native.WINEVENT_OUTOFCONTEXT | Native.WINEVENT_SKIPOWNPROCESS);

        hoverCheck.Tick += (_, _) => CheckHover();
        bar.Title = title;
        bar.MinimizeClicked += Minimize;
        bar.FullscreenClicked += ToggleFullscreen;
        bar.CloseClicked += Close;
        bar.Dragged += Move;
    }

    /// Title for the video window and its bar, normally the connected device's name.
    public string Title
    {
        get => title;
        set
        {
            title = value;
            bar.Title = value;
            if (video != IntPtr.Zero) SetTitle(video);
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

    void Adopt(IntPtr hwnd)
    {
        video = hwnd;
        fullscreen = null;
        bar.SetFullscreen(false);

        SetDwmAttribute(hwnd, Native.DWMWA_USE_IMMERSIVE_DARK_MODE, 1);
        SetDwmAttribute(hwnd, Native.DWMWA_BORDER_COLOR, 0x262626);           // Windows 11 only
        SetDwmAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, Native.DWMWCP_ROUND); // Windows 11 only
        Native.PostMessage(hwnd, Native.WM_SETICON, (IntPtr)Native.ICON_SMALL, smallIcon.Handle);
        Native.PostMessage(hwnd, Native.WM_SETICON, (IntPtr)Native.ICON_BIG, bigIcon.Handle);
        SetTitle(hwnd); // still shown in the taskbar and Alt+Tab

        // GStreamer sizes the client area to the video, so that size is the video's shape.
        Native.GetClientRect(hwnd, out var client);
        var videoSize = client.ToRectangle().Size;
        var style = Native.GetWindowLong(hwnd, Native.GWL_STYLE) & ~(Native.WS_CAPTION | Native.WS_SYSMENU | (KeepResizeEdges ? 0 : Native.WS_THICKFRAME));
        Native.SetWindowLong(hwnd, Native.GWL_STYLE, style);

        var area = Screen.FromHandle(hwnd).WorkingArea;
        var scale = Math.Min(1.0, Math.Min(area.Width * MaxScreenShare / Math.Max(1, videoSize.Width), area.Height * MaxScreenShare / Math.Max(1, videoSize.Height)));
        var fitted = new Size((int)(videoSize.Width * scale), (int)(videoSize.Height * scale));
        var frame = new Native.RECT(new Rectangle(Point.Empty, fitted));
        Native.AdjustWindowRectEx(ref frame, style, false, Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE));
        var outer = frame.ToRectangle();
        Native.SetWindowPos(hwnd, IntPtr.Zero,
            area.Left + (area.Width - fitted.Width) / 2 + outer.Left,
            area.Top + (area.Height - fitted.Height) / 2 + outer.Top,
            outer.Width, outer.Height,
            Native.SWP_FRAMECHANGED | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);

        hoverCheck.Start();
    }

    /// Shows the bar while the mouse is near the top of the visible video (or on the bar), hides it shortly after.
    void CheckHover()
    {
        if (!Native.IsWindow(video))
        {
            video = IntPtr.Zero;
            fullscreen = null;
            hoverCheck.Stop();
            bar.Conceal(immediate: true);
            return;
        }
        if (!Native.IsWindowVisible(video) || Native.IsIconic(video))
        {
            bar.Conceal(immediate: true);
            return;
        }

        var bounds = VisibleBounds();
        var cursor = Cursor.Position;
        var topmostAtCursor = Native.GetAncestor(Native.WindowFromPoint(new Native.POINT(cursor.X, cursor.Y)), Native.GA_ROOT);
        var onBar = bar.Visible && topmostAtCursor == bar.Handle;
        var inZone = topmostAtCursor == video && bounds.Contains(cursor) && cursor.Y < bounds.Top + bar.LogicalToDeviceUnits(RevealZone);

        if (onBar || inZone)
        {
            lastHover = DateTime.UtcNow;
            bar.Reveal(bounds);
        }
        else if (bar.Visible && DateTime.UtcNow - lastHover > HideDelay)
        {
            bar.Conceal();
        }
    }

    void Minimize()
    {
        bar.Conceal(immediate: true);
        Native.ShowWindowAsync(video, Native.SW_MINIMIZE);
    }

    /// Ends the mirroring session, like the window's own close button would; the receiver keeps running.
    void Close()
    {
        bar.Conceal(immediate: true);
        Native.PostMessage(video, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
    }

    /// Covers the whole monitor (taskbar included) and back. Done here rather than with GStreamer's Alt+Enter
    /// toggle, which only reacts to a physically pressed Enter key.
    void ToggleFullscreen()
    {
        if (fullscreen is { } saved)
        {
            Native.SetWindowLong(video, Native.GWL_STYLE, saved.Style);
            Native.SetWindowPlacement(video, ref saved.Placement);
            Native.SetWindowPos(video, IntPtr.Zero, 0, 0, 0, 0,
                Native.SWP_FRAMECHANGED | Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
            SetDwmAttribute(video, Native.DWMWA_WINDOW_CORNER_PREFERENCE, Native.DWMWCP_ROUND);
            fullscreen = null;
        }
        else
        {
            var placement = new Native.WINDOWPLACEMENT { Length = Marshal.SizeOf<Native.WINDOWPLACEMENT>() };
            Native.GetWindowPlacement(video, ref placement);
            var style = Native.GetWindowLong(video, Native.GWL_STYLE);
            fullscreen = new SavedFrame(style, placement);

            var screen = Screen.FromHandle(video).Bounds;
            Native.SetWindowLong(video, Native.GWL_STYLE, style & ~Native.WS_THICKFRAME);
            SetDwmAttribute(video, Native.DWMWA_WINDOW_CORNER_PREFERENCE, Native.DWMWCP_DONOTROUND);
            Native.SetWindowPos(video, IntPtr.Zero, screen.X, screen.Y, screen.Width, screen.Height,
                Native.SWP_FRAMECHANGED | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        }
        bar.SetFullscreen(fullscreen is not null);
        bar.Place(VisibleBounds());
    }

    void Move(Size delta)
    {
        if (fullscreen is not null) return;
        Native.GetWindowRect(video, out var rect);
        Native.SetWindowPos(video, IntPtr.Zero, rect.Left + delta.Width, rect.Top + delta.Height, 0, 0,
            Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE);
        bar.Place(VisibleBounds());
    }

    /// The window's visible edges; GetWindowRect would include the invisible resize borders.
    Rectangle VisibleBounds()
    {
        Native.DwmGetWindowAttribute(video, Native.DWMWA_EXTENDED_FRAME_BOUNDS, out var rect, Marshal.SizeOf<Native.RECT>());
        return rect.ToRectangle();
    }

    void SetTitle(IntPtr hwnd) =>
        // A timeout keeps a busy video thread from freezing the tray menu.
        Native.SendMessageTimeout(hwnd, Native.WM_SETTEXT, IntPtr.Zero, title, Native.SMTO_ABORTIFHUNG, 500, out _);

    static void SetDwmAttribute(IntPtr hwnd, int attribute, int value) =>
        // Fails harmlessly on Windows versions that lack the attribute.
        Native.DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));

    public void Dispose()
    {
        Native.UnhookWinEvent(hook);
        hoverCheck.Dispose();
        bar.Dispose();
        smallIcon.Dispose();
        bigIcon.Dispose();
    }
}
