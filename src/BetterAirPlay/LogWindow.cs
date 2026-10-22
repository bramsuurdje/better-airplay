using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace BetterAirPlay;

/// Live view of the receiver's output, for troubleshooting.
internal sealed class LogWindow : Form
{
    readonly TextBox text = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        Dock = DockStyle.Fill,
        Font = new Font("Consolas", 9f),
        BackColor = SystemColors.Window,
    };

    public LogWindow(IEnumerable<string> lines, Icon icon)
    {
        Text = $"{Program.AppName}: receiver log";
        Icon = icon;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(760, 440);
        text.KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.A) text.SelectAll(); // multiline TextBox lacks Ctrl+A
        };
        text.Lines = [.. lines];
        Controls.Add(text);
        Shown += (_, _) => ScrollToEnd();
    }

    public void AppendLine(string line) => text.AppendText(line + "\r\n");

    void ScrollToEnd()
    {
        text.SelectionStart = text.TextLength;
        text.ScrollToCaret();
    }
}
