using System.Drawing;
using System.Windows.Forms;

namespace BetterAirPlay;

/// Small dialog for changing the name Apple devices see.
internal sealed class NameDialog : Form
{
    readonly TextBox input = new() { Width = 320, MaxLength = Settings.MaxNameLength };
    readonly Label error = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(320, 0) };

    NameDialog(string current)
    {
        Text = $"{Program.AppName}: change name";
        Font = SystemFonts.MessageBoxFont;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = true;
        TopMost = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(12);

        input.Text = current;
        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        ok.Click += (_, _) =>
        {
            if (Settings.ValidateName(input.Text) is not { } problem) return;
            error.Text = problem;
            DialogResult = DialogResult.None;
        };
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([cancel, ok]);

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
        layout.Controls.Add(new Label { Text = "Name shown on your iPhone, iPad or Mac:", AutoSize = true });
        layout.Controls.Add(input);
        layout.Controls.Add(error);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
    }

    /// Returns the new, valid name, or null if the user cancelled.
    public static string? Ask(string current)
    {
        using var dialog = new NameDialog(current);
        return dialog.ShowDialog() == DialogResult.OK ? dialog.input.Text.Trim() : null;
    }
}
