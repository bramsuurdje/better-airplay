using System;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using Microsoft.Win32;

namespace BetterAirPlay;

/// User settings, stored under HKCU\Software\Better AirPlay.
internal sealed class Settings
{
    const string KeyPath = @"Software\" + Program.AppName;
    public const int MaxNameLength = 40;

    /// Name Apple devices show in their Screen Mirroring list.
    public string ReceiverName { get; set; } = Environment.MachineName;

    /// 4-digit code clients must enter once, or null when no code is required.
    public string? Pin { get; set; }

    /// Whether the receiver should run; remembered so the app resumes as the user left it.
    public bool Receiving { get; set; } = true;

    /// Whether to look for a newer release each time the app starts.
    public bool CheckForUpdates { get; set; } = true;

    public static Settings Load()
    {
        var settings = new Settings();
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        if (key is null) return settings;

        if (key.GetValue(nameof(ReceiverName)) is string name && ValidateName(name) is null) settings.ReceiverName = name;
        if (key.GetValue(nameof(Pin)) is string pin && IsPin(pin)) settings.Pin = pin;
        if (key.GetValue(nameof(Receiving)) is int receiving) settings.Receiving = receiving != 0;
        if (key.GetValue(nameof(CheckForUpdates)) is int check) settings.CheckForUpdates = check != 0;
        return settings;
    }

    public void Save()
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(nameof(ReceiverName), ReceiverName);
        key.SetValue(nameof(Pin), Pin ?? "");
        key.SetValue(nameof(Receiving), Receiving ? 1 : 0, RegistryValueKind.DWord);
        key.SetValue(nameof(CheckForUpdates), CheckForUpdates ? 1 : 0, RegistryValueKind.DWord);
    }

    /// Returns why a name can't be used, or null when it's valid.
    /// UxPlay reads its arguments in the system ANSI code page, so only printable ASCII survives the trip.
    public static string? ValidateName(string name)
    {
        if (name.Trim().Length == 0) return "Enter a name.";
        if (name.Length > MaxNameLength) return $"Use at most {MaxNameLength} characters.";
        if (name.Any(c => c < ' ' || c > '~')) return "Use only letters, digits, spaces and basic punctuation (no accents or emoji).";
        if (name.IndexOfAny(['"', '\\']) >= 0) return "Quotes and backslashes are not allowed.";
        return null;
    }

    public static string NewPin()
    {
        var bytes = new byte[4];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
        return (1000 + BitConverter.ToUInt32(bytes, 0) % 9000).ToString();
    }

    static bool IsPin(string value) => value.Length == 4 && value.All(char.IsDigit) && value != "0000";
}

/// "Start with Windows", via the per-user Run key.
internal static class Autostart
{
    const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static string Command => $"\"{Application.ExecutablePath}\"";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(Program.AppName) is string command && string.Equals(command, Command, StringComparison.OrdinalIgnoreCase);
        }
        set
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (value) key.SetValue(Program.AppName, Command);
            else key.DeleteValue(Program.AppName, throwOnMissingValue: false);
        }
    }
}
