using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;

namespace BetterAirPlay;

/// A published release that is newer than the running app.
internal sealed class Update(Version version, Uri installerUrl)
{
    public Version Version { get; } = version;
    public Uri InstallerUrl { get; } = installerUrl;
}

/// Finds and installs new releases from GitHub. .github/workflows/build.yml publishes them tagged
/// v<version>, with the installer attached as BetterAirPlay-Setup-<version>.exe.
internal static class Updater
{
    const string Repository = "bramsuurdje/better-airplay-windows";
    const int ErrorCancelled = 1223; // the user said no to the admin prompt

    /// Version of the running app; CI stamps it from the release number.
    public static readonly Version CurrentVersion = ThreePart(typeof(Updater).Assembly.GetName().Version);

    static readonly HttpClient Http = CreateClient();

    /// Returns the latest release if it's newer than this app, otherwise null.
    public static async Task<Update?> FindAsync()
    {
        using var response = await Http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest");
        response.EnsureSuccessStatusCode();
        using var body = await response.Content.ReadAsStreamAsync();
        var latest = (GitHubRelease)new DataContractJsonSerializer(typeof(GitHubRelease)).ReadObject(body);

        if (!Version.TryParse(latest.TagName.TrimStart('v'), out var version)) throw new InvalidDataException($"Unexpected release tag {latest.TagName}.");
        if (ThreePart(version) <= CurrentVersion) return null;

        var installerName = $"BetterAirPlay-Setup-{version}.exe";
        var installer = latest.Assets.FirstOrDefault(asset => asset.Name == installerName)
            ?? throw new InvalidDataException($"Release {latest.TagName} has no {installerName}.");
        return new Update(version, new Uri(installer.DownloadUrl));
    }

    /// Downloads the installer and starts it with only a progress window. The installer replaces this app and
    /// starts the new version, so the caller should quit when this returns true.
    /// Returns false when the user declines the admin prompt.
    public static async Task<bool> InstallAsync(Update update)
    {
        var installerPath = Path.Combine(Path.GetTempPath(), $"BetterAirPlay-Setup-{update.Version}.exe");
        using (var response = await Http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            using var file = File.Create(installerPath);
            await response.Content.CopyToAsync(file);
        }

        // Keep "Start with Windows" as the user last set it; otherwise the installer restores its own choice.
        var autostart = Autostart.Enabled ? "autostart" : "!autostart";
        var info = new ProcessStartInfo(installerPath, $"/SILENT /SUPPRESSMSGBOXES /NORESTART /MERGETASKS=\"{autostart}\"")
        {
            UseShellExecute = true, // lets Windows show the admin prompt the installer asks for
        };
        try
        {
            Process.Start(info)?.Dispose();
            return true;
        }
        catch (Win32Exception e) when (e.NativeErrorCode == ErrorCancelled)
        {
            return false;
        }
    }

    static Version ThreePart(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"BetterAirPlay/{CurrentVersion}"); // GitHub rejects requests without one
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    [DataContract]
    sealed class GitHubRelease
    {
        [DataMember(Name = "tag_name")] public string TagName = "";
        [DataMember(Name = "assets")] public GitHubAsset[] Assets = [];
    }

    [DataContract]
    sealed class GitHubAsset
    {
        [DataMember(Name = "name")] public string Name = "";
        [DataMember(Name = "browser_download_url")] public string DownloadUrl = "";
    }
}
