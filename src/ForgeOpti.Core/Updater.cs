using System.Diagnostics;
using System.Security.Cryptography;

namespace ForgeOpti.Core;

/// <summary>Mise à jour automatique : manifeste signé (clé publique épinglée) -> zip -> SHA-256 -> staging à ACL admin -> remplacement après fermeture -> relance.</summary>
public static class Updater
{
    const string AssetZip = "ForgeOpti-win-x64.zip";
    static readonly HttpClient Http = new() { DefaultRequestHeaders = { { "User-Agent", "ForgeOpti-Updater" } }, Timeout = TimeSpan.FromMinutes(5) };

    public static bool Enabled
    {
        get => Settings.Get("autoUpdate", true);
        set => Settings.Set("autoUpdate", value);
    }

    public static Version Current => typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>null = déjà à jour. Lève si la clé n'est pas configurée ou si la signature est invalide.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        var manifest = await Http.GetByteArrayAsync(UpdateVerifier.ManifestUrl);
        var sig = await Http.GetByteArrayAsync(UpdateVerifier.ManifestUrl + ".sig");
        var info = UpdateVerifier.Parse(manifest, sig);
        return info.Version > Current ? info : null;
    }

    /// <summary>Télécharge, vérifie l'intégrité, puis lance le remplacement et quitte. Lève si l'empreinte ne correspond pas.</summary>
    public static async Task InstallAsync(UpdateInfo u)
    {
        var work = PrivateDir.Ensure(Path.Combine(Log.Dir, "update-" + Guid.NewGuid().ToString("N")));
        var zip = Path.Combine(work, AssetZip);
        try
        {
            using var resp = await Http.GetAsync(u.Url, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            await using (var s = await resp.Content.ReadAsStreamAsync())
            await using (var f = File.Create(zip)) await s.CopyToAsync(f);
            string actual;
            await using (var f = File.OpenRead(zip)) actual = Convert.ToHexString(await SHA256.HashDataAsync(f)).ToLowerInvariant();
            if (actual != u.Sha256 || new FileInfo(zip).Length != u.Size) throw new InvalidDataException("Empreinte SHA-256 invalide : mise à jour refusée.");
        }
        catch { Directory.Delete(work, true); throw; }

        var ps = UpdateVerifier.BuildScript(Environment.ProcessId, zip, u.Sha256, Path.Combine(work, "new"), AppContext.BaseDirectory.TrimEnd('\\'), Environment.ProcessPath!, work);
        Process.Start(new ProcessStartInfo("powershell.exe", $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -EncodedCommand {Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(ps))}") { CreateNoWindow = true, UseShellExecute = false });
        Environment.Exit(0);
    }
}
