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
    readonly ToolStripMenuItem lowLatencyItem = new("Low latency (screen mirroring)") { ToolTipText = "Turn off to keep sound in sync with video when watching films." };
    readonly ToolStripMenuItem autostartItem = new("Start with Windows");
    readonly ToolStripMenuItem checkAtStartupItem = new("Check for updates at startup");
    readonly ToolStripMenuItem updateItem = new();
    LogWindow? logWindow;
    Update? availableUpdate;
    bool installingUpdate;
    Action? onBalloonClick;

    public TrayApp()
    {
        videoWindow = new VideoWindowStyler(() => receiver.ProcessId);
        toggleItem.Click += (_, _) => SetReceiving(!receiver.IsRunning);
        pinItem.Click += (_, _) => TogglePin();
        lowLatencyItem.Click += (_, _) => ToggleLowLatency();
        autostartItem.Click += (_, _) => Autostart.Enabled = !Autostart.Enabled;
        checkAtStartupItem.Click += (_, _) => ToggleCheckAtStartup();
        updateItem.Click += (_, _) =>
        {
            if (availableUpdate is null) CheckForUpdates(manual: true);
            else InstallUpdate();
        };

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
            lowLatencyItem,
            autostartItem,
            checkAtStartupItem,
            new ToolStripSeparator(),
            updateItem,
            new ToolStripMenuItem("Show log", null, (_, _) => ShowLog()),
            new ToolStripMenuItem("Quit", null, (_, _) => Quit()),
        ]);
        // Autostart can also be changed from Task Manager, so read it fresh each time.
        menu.Opening += (_, _) => autostartItem.Checked = Autostart.Enabled;

        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => SetReceiving(!receiver.IsRunning);
        tray.BalloonTipClicked += (_, _) => onBalloonClick?.Invoke();
        tray.BalloonTipClosed += (_, _) => onBalloonClick = null;
        receiver.LineReceived += OnReceiverLine;
        receiver.Crashed += OnReceiverCrashed;

        Receiver.KillOrphans();
        if (settings.Receiving) StartReceiver();
        Refresh();
        tray.Visible = true;
        if (settings.CheckForUpdates) CheckForUpdates(manual: false);
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
            receiver.Start(settings.ReceiverName, settings.Pin, settings.LowLatency);
        }
        catch (Exception e)
        {
            ShowError("Could not start the receiver.", e);
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

    void ToggleLowLatency()
    {
        settings.LowLatency = !settings.LowLatency;
        settings.Save();
        Restart();
        Refresh();
    }

    void ToggleCheckAtStartup()
    {
        settings.CheckForUpdates = !settings.CheckForUpdates;
        settings.Save();
        Refresh();
    }

    /// Looks for a newer release. A manual check always reports back; the startup check only speaks up
    /// when there is an update.
    async void CheckForUpdates(bool manual)
    {
        try
        {
            availableUpdate = await Updater.FindAsync();
        }
        catch (Exception e) // async void: anything unhandled here would crash the app
        {
            if (manual) ShowError("Could not check for updates.", e);
            return;
        }
        Refresh();

        if (availableUpdate is not { } update)
        {
            if (manual) MessageBox.Show($"You have the latest version ({Updater.CurrentVersion}).", Program.AppName);
            return;
        }
        if (!manual)
        {
            Notify($"Update available: {update.Version}", "Click here to install it.", onClick: InstallUpdate);
            return;
        }
        var answer = MessageBox.Show(
            $"Version {update.Version} is available. You have {Updater.CurrentVersion}.\n\nInstall it now? The receiver stops briefly.",
            Program.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer == DialogResult.Yes) InstallUpdate();
    }

    async void InstallUpdate()
    {
        if (availableUpdate is not { } update || installingUpdate) return;
        installingUpdate = true;
        Refresh();
        Notify($"Downloading version {update.Version}", "It installs as soon as the download finishes.");
        try
        {
            if (await Updater.InstallAsync(update))
            {
                Quit(); // the installer starts the new version when it's done
                return;
            }
        }
        catch (Exception e) // async void: anything unhandled here would crash the app
        {
            ShowError("Could not install the update.", e);
        }
        installingUpdate = false;
        Refresh();
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
        lowLatencyItem.Checked = settings.LowLatency;
        checkAtStartupItem.Checked = settings.CheckForUpdates;
        updateItem.Enabled = !installingUpdate;
        updateItem.Text = (installingUpdate, availableUpdate) switch
        {
            (true, _) => "Downloading update…",
            (false, { } update) => $"Install update {update.Version}",
            (false, null) => "Check for updates…",
        };
        tray.Icon = running ? onIcon : offIcon;

        var tooltip = running ? $"{Program.AppName}: {settings.ReceiverName}" : $"{Program.AppName}: off";
        tray.Text = tooltip.Length > 63 ? tooltip.Substring(0, 63) : tooltip; // NotifyIcon throws above 63 chars
    }

    void Notify(string title, string text, ToolTipIcon icon = ToolTipIcon.Info, Action? onClick = null)
    {
        onBalloonClick = onClick;
        tray.ShowBalloonTip(5000, title, text.Length == 0 ? title : text, icon);
    }

    static void ShowError(string message, Exception e) =>
        MessageBox.Show($"{message}\n\n{e.Message}", Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);

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
