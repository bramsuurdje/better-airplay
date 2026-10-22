using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace BetterAirPlay;

/// Runs the bundled UxPlay receiver (receiver\bin\uxplay.exe next to this app) and collects its output.
/// Create it on the UI thread: events are raised there.
internal sealed class Receiver
{
    const int MaxLogLines = 1000;
    static readonly string BinDir = Path.Combine(AppContext.BaseDirectory, "receiver", "bin");
    static readonly string ExePath = Path.Combine(BinDir, "uxplay.exe");
    static readonly string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Program.AppName);

    readonly SynchronizationContext ui = SynchronizationContext.Current
        ?? throw new InvalidOperationException("Create the receiver on the UI thread.");
    readonly Queue<string> log = new();
    Process? process;

    /// A line of receiver output was added to Log.
    public event Action<string>? LineReceived;

    /// The receiver quit on its own (not via Stop), with this exit code.
    public event Action<int>? Crashed;

    public bool IsRunning => process is not null;
    public IEnumerable<string> Log => log;

    public void Start(string name, string? pin)
    {
        if (process is not null) throw new InvalidOperationException("The receiver is already running.");
        if (!File.Exists(ExePath)) throw new FileNotFoundException($"The receiver is missing. Reinstall {Program.AppName}.", ExePath);
        Directory.CreateDirectory(DataDir);

        // Settings.ValidateName rules out quotes and backslashes, so plain quoting is safe.
        var args = $"-n \"{name}\" -nh -vs d3d11videosink" + (pin is null ? "" : $" -pin {pin}");
        var info = new ProcessStartInfo(ExePath, args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = BinDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // Pin GStreamer to the bundled plugins (see scripts/bundle-receiver.sh for the layout).
        var env = info.EnvironmentVariables;
        env["GST_PLUGIN_SYSTEM_PATH_1_0"] = Path.GetFullPath(Path.Combine(BinDir, "..", "lib", "gstreamer-1.0"));
        env["GST_PLUGIN_SCANNER_1_0"] = Path.Combine(BinDir, "gst-plugin-scanner.exe");
        env["GST_REGISTRY_1_0"] = Path.Combine(DataDir, "gstreamer-registry.bin");
        env.Remove("GST_PLUGIN_PATH_1_0");
        env.Remove("GST_PLUGIN_PATH");

        var started = new Process { StartInfo = info, EnableRaisingEvents = true };
        started.OutputDataReceived += (_, e) => Post(e.Data);
        started.ErrorDataReceived += (_, e) => Post(e.Data);
        started.Exited += (_, _) => ui.Post(_ => OnExited(started), null);
        Append($"> uxplay {args}");
        started.Start();
        started.BeginOutputReadLine();
        started.BeginErrorReadLine();
        process = started;
    }

    public void Stop()
    {
        if (process is not { } running) return;
        process = null;
        try
        {
            if (!running.HasExited)
            {
                running.Kill();
                running.WaitForExit(3000);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception) { /* already gone */ }
        running.Dispose();
        Append("> receiver stopped");
    }

    /// Kills receivers left behind by a previous run of this app that didn't shut down cleanly,
    /// so they don't advertise a stale name alongside the new one.
    public static void KillOrphans()
    {
        foreach (var candidate in Process.GetProcessesByName("uxplay"))
        {
            using (candidate)
            {
                try
                {
                    if (string.Equals(candidate.MainModule?.FileName, ExePath, StringComparison.OrdinalIgnoreCase)) candidate.Kill();
                }
                catch (Exception e) when (e is InvalidOperationException or Win32Exception) { /* exited or not ours */ }
            }
        }
    }

    void Post(string? line)
    {
        if (line is not null) ui.Post(_ => Append(line), null);
    }

    void Append(string line)
    {
        log.Enqueue(line);
        while (log.Count > MaxLogLines) log.Dequeue();
        LineReceived?.Invoke(line);
    }

    void OnExited(Process exited)
    {
        if (exited != process) return; // stopped on purpose
        process = null;
        var code = exited.ExitCode;
        exited.Dispose();
        Append($"> receiver exited with code {code}");
        Crashed?.Invoke(code);
    }
}
