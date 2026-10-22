using System;
using System.Threading;
using System.Windows.Forms;

namespace BetterAirPlay;

internal static class Program
{
    public const string AppName = "Better AirPlay";

    [STAThread]
    static void Main()
    {
        using var instance = new Mutex(true, @"Local\BetterAirPlay", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show($"{AppName} is already running. Look for its icon in the notification area.", AppName);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // Receiver events are posted to this context, so it must exist before TrayApp is built.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        Application.Run(new TrayApp());
    }
}
