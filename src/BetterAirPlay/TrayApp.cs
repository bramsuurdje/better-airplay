using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace BetterAirPlay;

/// The whole app: a notification-area icon whose menu controls the receiver.
internal sealed class TrayApp : ApplicationContext
{
    // UxPlay logs "connection request from <device name> (<model>) with deviceID = ..." when a client connects.
    static readonly Regex ConnectionRequest = new(@"connection request from (.+?) \(", RegexOptions.Compiled);

    readonly Settings settings = Settings.Load();
    readonly Receiver receiver = new();
    readonly Icon onIcon = LoadIcon("app.ico", SystemInformation.SmallIconSize);
    readonly Icon offIcon = LoadIcon("app-off.ico", SystemInformation.SmallIconSize);
    readonly VideoWindowStyler videoWindow;
    readonly NotifyIcon tray = new();
    readonly ToolStripMenuItem statusItem = new() { Enabled = false };
    readonly ToolStripMenuItem codeItem = new() { Enabled = false };
    readonly ToolStripMenuItem toggleItem = new();
    readonly ToolStripMenuItem pinItem = new("Require connection code");
    readonly ToolStripMenuItem autostartItem = new("Start with Windows");
    LogWindow? logWindow;

    public TrayApp()
    {
        videoWindow = new VideoWindowStyler(() => receiver.ProcessId);
        toggleItem.Click += (_, _) => SetReceiving(!receiver.IsRunning);
        pinItem.Click += (_, _) => TogglePin();
        autostartItem.Click += (_, _) => Autostart.Enabled = !Autostart.Enabled;

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            statusItem,
            codeItem,
            new ToolStripSeparator(),
            toggleItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Change name…", null, (_, _) => Rename()),
            pinItem,
            autostartItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Show log", null, (_, _) => ShowLog()),
            new ToolStripMenuItem("Quit", null, (_, _) => Quit()),
        ]);
        // Autostart can also be changed from Task Manager, so read it fresh each time.
        menu.Opening += (_, _) => autostartItem.Checked = Autostart.Enabled;

        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => SetReceiving(!receiver.IsRunning);
        receiver.LineReceived += OnReceiverLine;
        receiver.Crashed += OnReceiverCrashed;

        Receiver.KillOrphans();
        if (settings.Receiving) StartReceiver();
        Refresh();
        tray.Visible = true;
    }

    void SetReceiving(bool receiving)
    {
        settings.Receiving = receiving;
        settings.Save();
        if (receiving) StartReceiver();
        else receiver.Stop();
        Refresh();
    }

    void StartReceiver()
    {
        try
        {
            receiver.Start(settings.ReceiverName, settings.Pin);
        }
        catch (Exception e)
        {
            MessageBox.Show($"Could not start the receiver.\n\n{e.Message}", Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// Applies changed settings to a running receiver.
    void Restart()
    {
        if (!receiver.IsRunning) return;
        receiver.Stop();
        StartReceiver();
    }

    void Rename()
    {
        if (NameDialog.Ask(settings.ReceiverName) is not { } name || name == settings.ReceiverName) return;
        settings.ReceiverName = name;
        settings.Save();
        Restart();
        Refresh();
    }

    void TogglePin()
    {
        settings.Pin = settings.Pin is null ? Settings.NewPin() : null;
        settings.Save();
        Restart();
        Refresh();
        if (settings.Pin is not null)
        {
            Notify($"Connection code: {settings.Pin}", "Devices enter this code the first time they connect.");
        }
    }

    void ShowLog()
    {
        if (logWindow is null)
        {
            logWindow = new LogWindow(receiver.Log, onIcon);
            logWindow.FormClosed += (_, _) => logWindow = null;
            logWindow.Show();
        }
        logWindow.Activate();
    }

    void OnReceiverLine(string line)
    {
        logWindow?.AppendLine(line);
        if (ConnectionRequest.Match(line) is { Success: true } match)
        {
            var device = match.Groups[1].Value;
            videoWindow.Title = device;
            Notify($"{device} is connecting", settings.Pin is null ? "" : $"If asked, enter code {settings.Pin}.");
        }
    }

    void OnReceiverCrashed(int exitCode)
    {
        Refresh();
        Notify("The receiver stopped unexpectedly", $"Exit code {exitCode}. Open the log for details.", ToolTipIcon.Warning);
    }

    void Refresh()
    {
        var running = receiver.IsRunning;
        statusItem.Text = running ? $"Receiving as “{settings.ReceiverName}”" : "Not receiving";
        codeItem.Text = $"Connection code: {settings.Pin}";
        codeItem.Visible = running && settings.Pin is not null;
        toggleItem.Text = running ? "Stop receiving" : "Start receiving";
        pinItem.Checked = settings.Pin is not null;
        tray.Icon = running ? onIcon : offIcon;

        var tooltip = running ? $"{Program.AppName}: {settings.ReceiverName}" : $"{Program.AppName}: off";
        tray.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip; // NotifyIcon throws above 63 chars
    }

    void Notify(string title, string text, ToolTipIcon icon = ToolTipIcon.Info) =>
        tray.ShowBalloonTip(5000, title, text.Length == 0 ? title : text, icon);

    void Quit()
    {
        receiver.Stop();
        tray.Visible = false;
        logWindow?.Close();
        videoWindow.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            receiver.Stop();
            tray.Dispose();
            onIcon.Dispose();
            offIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    internal static Icon LoadIcon(string resource, Size size)
    {
        using var stream = typeof(TrayApp).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded resource {resource}.");
        return new Icon(stream, size);
    }
}
