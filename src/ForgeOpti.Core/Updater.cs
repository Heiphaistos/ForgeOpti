using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace ForgeOpti.Core;

public sealed record UpdateInfo(Version Version, string ZipUrl, string Sha256);

/// <summary>Mise à jour automatique : release GitHub -> téléchargement -> vérif SHA-256 -> remplacement après fermeture -> relance.</summary>
public static class Updater
{
    static readonly string Api = Environment.GetEnvironmentVariable("FORGEOPTI_UPDATE_API") ?? "https://api.github.com/repos/Heiphaistos/ForgeOpti/releases/latest";
    const string AssetZip = "ForgeOpti-win-x64.zip";
    static readonly HttpClient Http = new() { DefaultRequestHeaders = { { "User-Agent", "ForgeOpti-Updater" } }, Timeout = TimeSpan.FromMinutes(5) };

    public static bool Enabled
    {
        get => Settings.Get("autoUpdate", true);
        set => Settings.Set("autoUpdate", value);
    }

    public static Version Current => typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>null = déjà à jour (ou release sans asset valide).</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var doc = JsonDocument.Parse(await Http.GetStringAsync(Api));
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString()!.TrimStart('v');
        if (!Version.TryParse(tag, out var v) || v <= Current) return null;
        string? zip = null, sum = null;
        foreach (var a in root.GetProperty("assets").EnumerateArray())
        {
            var n = a.GetProperty("name").GetString();
            var u = a.GetProperty("browser_download_url").GetString();
            if (n == AssetZip) zip = u; else if (n == AssetZip + ".sha256") sum = u;
        }
        if (zip is null || sum is null) return null; // pas d'empreinte publiée : on n'installe jamais
        var hash = (await Http.GetStringAsync(sum)).Trim().Split(' ')[0].ToLowerInvariant();
        return new UpdateInfo(v, zip, hash);
    }

    /// <summary>Télécharge, vérifie l'intégrité, puis lance le remplacement et quitte. Lève si l'empreinte ne correspond pas.</summary>
    public static async Task InstallAsync(UpdateInfo u)
    {
        var tmp = Path.Combine(Path.GetTempPath(), "forgeopti-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var zip = Path.Combine(tmp, AssetZip);
        await using (var s = await Http.GetStreamAsync(u.ZipUrl))
        await using (var f = File.Create(zip)) await s.CopyToAsync(f);
        string actual;
        await using (var f = File.OpenRead(zip)) actual = Convert.ToHexString(await SHA256.HashDataAsync(f)).ToLowerInvariant();
        if (actual != u.Sha256) { Directory.Delete(tmp, true); throw new InvalidDataException("Empreinte SHA-256 invalide : mise à jour refusée."); }

        var stage = Path.Combine(tmp, "new");
        ZipFile.ExtractToDirectory(zip, stage);
        var exe = Environment.ProcessPath!;
        var dir = AppContext.BaseDirectory.TrimEnd('\\');
        // Script détaché : attend la fermeture de l'appli, copie, relance.
        var ps = $"Wait-Process -Id {Environment.ProcessId} -ErrorAction SilentlyContinue; " +
                 $"Copy-Item -Path '{stage}\\*' -Destination '{dir}' -Recurse -Force; " +
                 $"Start-Process '{exe}'; Remove-Item '{tmp}' -Recurse -Force";
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand {Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(ps))}") { CreateNoWindow = true, UseShellExecute = false });
        Environment.Exit(0);
    }
}
