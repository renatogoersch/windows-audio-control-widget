using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SlotMixer;

/// <summary>A published release newer than the running version, with the zip that matches how this copy was built.</summary>
internal sealed record UpdateInfo(Version Version, string Tag, string AssetName, string AssetUrl, long AssetSize, string? Sha256);

/// <summary>
/// Finds, downloads and installs new versions from the GitHub Releases of the project: no server of our own, only the
/// public "latest release" API and the zips attached to it. Installing is the same forced <c>--install</c> the exe
/// already does, run by the freshly downloaded copy.
/// </summary>
internal static class Updater
{
    public const string Repository = "renatogoersch/windows-audio-control-widget";

    /// <summary>Page with every release, for copies that are not installed (those are never replaced behind the user's back).</summary>
    public static string ReleasesPage => $"https://github.com/{Repository}/releases/latest";

    /// <summary>SLOTMIXER_UPDATE_FEED replaces the GitHub feed (a local server in tests, or a fork's address).</summary>
    static readonly string? FeedOverride = Environment.GetEnvironmentVariable("SLOTMIXER_UPDATE_FEED");

    static readonly HttpClient Http = CreateClient();

    static string FeedUrl => FeedOverride ?? $"https://api.github.com/repos/{Repository}/releases/latest";

    static string WorkFolder => Path.Combine(Path.GetTempPath(), "SlotMixer-update");

    /// <summary>
    /// True when this exe carries its own .NET (the "autônomo" zip). Set at build time, so an update picks the zip of the
    /// same kind: sending the small one to someone without the runtime would break their install.
    /// </summary>
    public static bool IsSelfContained { get; } = typeof(Updater).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .Any(a => a.Key == "SelfContained" && string.Equals(a.Value, "true", StringComparison.OrdinalIgnoreCase));


    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"SlotMixer/{Installer.CurrentVersion.ToString(3)}");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    /// <summary>The newest release when it is newer than this copy; null when this copy is up to date. Throws when there is no answer.</summary>
    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancel)
    {
        var json = await Http.GetStringAsync(FeedUrl, cancel);
        return Parse(json, Installer.CurrentVersion, IsSelfContained, FeedOverride != null);
    }

    /// <summary>
    /// Reads the "latest release" answer of GitHub: the release when it is newer than <paramref name="current"/> and has the
    /// zip of the right kind (from this project's own downloads, unless <paramref name="trustAnyUrl"/>), otherwise null.
    /// </summary>
    internal static UpdateInfo? Parse(string json, Version current, bool selfContained, bool trustAnyUrl)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!TryParseTag(tag, out var version) || version <= Normalize(current))
            return null;

        if (!root.TryGetProperty("assets", out var assets))
            return null;

        var wanted = selfContained ? "SlotMixer-autonomo.zip" : "SlotMixer-requer-dotnet.zip";
        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.GetProperty("name").GetString() != wanted)
                continue;

            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (!trustAnyUrl && !url.StartsWith($"https://github.com/{Repository}/releases/download/", StringComparison.OrdinalIgnoreCase))
                return null;

            var size = asset.TryGetProperty("size", out var sizeElement) ? sizeElement.GetInt64() : 0;
            string? sha256 = null;
            if (asset.TryGetProperty("digest", out var digest) && digest.GetString() is { } text && text.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                sha256 = text["sha256:".Length..];

            return new UpdateInfo(version, tag, wanted, url, size, sha256);
        }
        return null;
    }

    /// <summary>
    /// Downloads the zip, checks it (size, SHA-256 when GitHub gives one, and that the exe inside really is the announced
    /// version) and returns the path of the extracted exe.
    /// </summary>
    public static async Task<string> DownloadAsync(UpdateInfo info, CancellationToken cancel)
    {
        var folder = Path.Combine(WorkFolder, info.Tag);
        try
        {
            return await DownloadCoreAsync(info, folder, cancel);
        }
        catch (Exception)
        {
            // Whatever was rejected (a bad checksum, a cut connection) must not be left lying around.
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (Exception)
            {
                // Cleaned up by the next run.
            }
            throw;
        }
    }

    static async Task<string> DownloadCoreAsync(UpdateInfo info, string folder, CancellationToken cancel)
    {
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        Directory.CreateDirectory(folder);

        var zipPath = Path.Combine(folder, info.AssetName);
        using (var response = await Http.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead, cancel))
        {
            response.EnsureSuccessStatusCode();
            await using var file = File.Create(zipPath);
            await response.Content.CopyToAsync(file, cancel);
        }

        if (info.AssetSize > 0 && new FileInfo(zipPath).Length != info.AssetSize)
            throw new InvalidDataException("O arquivo baixado tem tamanho diferente do anunciado.");

        if (info.Sha256 != null)
        {
            await using var file = File.OpenRead(zipPath);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancel));
            if (!hash.Equals(info.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A soma SHA-256 do arquivo baixado não confere.");
        }

        var exePath = Path.Combine(folder, "SlotMixer.exe");
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var entry = zip.Entries.FirstOrDefault(e => e.Name.Equals("SlotMixer.exe", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("O zip não tem o SlotMixer.exe.");
            entry.ExtractToFile(exePath, overwrite: true);
        }

        if (!Version.TryParse(FileVersionInfo.GetVersionInfo(exePath).FileVersion, out var inside) || Normalize(inside) != info.Version)
            throw new InvalidDataException("O exe do zip não é da versão anunciada.");

        return exePath;
    }

    /// <summary>Starts the downloaded exe in install mode; it stops this copy, replaces it and starts the new one. The caller exits next.</summary>
    public static void Launch(string exePath) =>
        Process.Start(new ProcessStartInfo(exePath, "--install") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exePath)! });

    /// <summary>Removes what earlier updates left in the temp folder. Never fails.</summary>
    public static void CleanUp()
    {
        try
        {
            if (Directory.Exists(WorkFolder))
                Directory.Delete(WorkFolder, recursive: true);
        }
        catch (Exception)
        {
            // A file still in use (an update that is starting) is simply left for the next run.
        }
    }

    static bool TryParseTag(string tag, out Version version)
    {
        var ok = Version.TryParse(tag.TrimStart('v', 'V'), out var parsed);
        version = ok ? Normalize(parsed!) : new Version(0, 0, 0);
        return ok;
    }

    /// <summary>Major.minor.patch only: "1.5.0.0" from the assembly and "1.5.0" from a tag are the same version.</summary>
    static Version Normalize(Version version) => new(version.Major, version.Minor, Math.Max(version.Build, 0));
}
