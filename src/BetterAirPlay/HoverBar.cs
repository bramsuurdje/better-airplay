using System;
using System.Drawing;
using System.Windows.Forms;

namespace BetterAirPlay;

/// The title bar that slides down over the top of the video when the mouse gets there.
/// It only draws and reports clicks; VideoWindowChrome decides when to show it and acts on the clicks.
internal sealed class HoverBar : Form
{
    const int LogicalHeight = 36;
    const int AnimationMs = 140;
    static readonly Color Background = Color.FromArgb(24, 24, 24);
    static readonly Color Hover = Color.FromArgb(55, 55, 55);
    static readonly Color CloseHover = Color.FromArgb(196, 43, 28); // Windows' own close-button red

    readonly Panel content = new();
    readonly Label title = new();
    readonly Button fullscreenButton;
    readonly Timer animation = new() { Interval = 15 };
    DateTime lastFrame;
    double position; // 0 = hidden, 1 = fully down; moves linearly with time and is eased when drawn
    bool revealing;  // the direction the bar is heading
    Point? pressedAt; // where a left press on the title happened, until it is released or becomes a drag

    public event Action? MinimizeClicked;
    public event Action? FullscreenClicked;
    public event Action? CloseClicked;

    /// The user started dragging the bar with the left button still down; the video window should take over the drag.
    public event Action? DragStarted;

    public HoverBar()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Background;
        AutoScaleMode = AutoScaleMode.None; // sizes below are already converted with LogicalToDeviceUnits

        title.Dock = DockStyle.Fill;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Padding = new Padding(12, 0, 0, 0);
        title.ForeColor = Color.FromArgb(220, 220, 220);
        title.Font = new Font("Segoe UI", 9f);
        title.AutoEllipsis = true;
        title.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) pressedAt = Cursor.Position;
        };
        title.MouseUp += (_, _) => pressedAt = null;
        title.MouseMove += (_, _) =>
        {
            // Wait for the drag threshold so a click or double-click stays a click.
            if (pressedAt is not { } origin) return;
            var threshold = SystemInformation.DragSize;
            var now = Cursor.Position;
            if (Math.Abs(now.X - origin.X) <= threshold.Width / 2 && Math.Abs(now.Y - origin.Y) <= threshold.Height / 2) return;
            pressedAt = null;
            title.Capture = false; // the video window's move loop takes the mouse from here
            DragStarted?.Invoke();
        };
        title.DoubleClick += (_, _) => FullscreenClicked?.Invoke();

        // Glyphs from Segoe MDL2 Assets, the icon font Windows' own title bars use.
        fullscreenButton = CaptionButton("", Hover, () => FullscreenClicked?.Invoke());
        content.Controls.Add(title);
        content.Controls.Add(CaptionButton("", Hover, () => MinimizeClicked?.Invoke()));
        content.Controls.Add(fullscreenButton);
        content.Controls.Add(CaptionButton("", CloseHover, () => CloseClicked?.Invoke()));
        content.Height = BarHeight;
        content.Dock = DockStyle.None;
        Controls.Add(content);

        animation.Tick += (_, _) => Animate();
    }

    public int BarHeight => LogicalToDeviceUnits(LogicalHeight);

    public string Title
    {
        get => title.Text;
        set => title.Text = value;
    }

    public void SetFullscreen(bool fullscreen) =>
        fullscreenButton.Text = fullscreen ? "" : ""; // "back to window" / "full screen"

    /// Slides the bar down along the top edge of the given area. Safe to call repeatedly; if the bar is sliding
    /// up, it turns around from where it is.
    public void Reveal(Rectangle videoBounds)
    {
        Place(videoBounds);
        if (revealing) return;
        revealing = true;
        if (!Visible)
        {
            position = 0;
            Draw();
            Show();
        }
        StartAnimation();
    }

    /// Slides the bar back up; with immediate, it just disappears. Safe to call repeatedly.
    public void Conceal(bool immediate = false)
    {
        if (!Visible) return;
        if (immediate)
        {
            revealing = false;
            animation.Stop();
            position = 0;
            Hide();
            return;
        }
        if (!revealing) return; // already on its way up
        revealing = false;
        StartAnimation();
    }

    /// Keeps the bar lined up with the video window, e.g. while it is dragged or snapped.
    public void Place(Rectangle videoBounds)
    {
        Location = videoBounds.Location;
        Width = videoBounds.Width;
        content.Width = videoBounds.Width;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE; // no Alt+Tab entry, never steals focus
            return cp;
        }
    }

    void StartAnimation()
    {
        if (animation.Enabled) return; // a running animation just follows the new direction
        lastFrame = DateTime.UtcNow;
        animation.Start();
    }

    void Animate()
    {
        var now = DateTime.UtcNow;
        var step = (now - lastFrame).TotalMilliseconds / AnimationMs;
        lastFrame = now;
        position = revealing ? Math.Min(1, position + step) : Math.Max(0, position - step);
        Draw();
        if (revealing ? position < 1 : position > 0) return;
        animation.Stop();
        if (!revealing) Hide();
    }

    void Draw()
    {
        var eased = 1 - Math.Pow(1 - position, 3); // ease-out cubic going down, ease-in going up
        var height = Math.Max(1, (int)Math.Round(BarHeight * eased));
        Height = height;
        content.Top = height - BarHeight; // the content slides down with the bottom edge
    }

    Button CaptionButton(string glyph, Color hover, Action onClick)
    {
        var button = new Button
        {
            Text = glyph,
            Font = new Font("Segoe MDL2 Assets", 10f),
            ForeColor = Color.FromArgb(230, 230, 230),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Width = LogicalToDeviceUnits(46),
            TabStop = false,
            UseVisualStyleBackColor = false,
            BackColor = Background,
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = hover;
        button.FlatAppearance.MouseDownBackColor = hover;
        button.Click += (_, _) => onClick();
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) animation.Dispose();
        base.Dispose(disposing);
    }
}
