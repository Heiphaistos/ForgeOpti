using System.Security.Cryptography;
using System.Text.Json;

namespace ForgeOpti.Core;

/// <summary>Mise à jour annoncée par un manifeste dont la signature est valide.</summary>
public sealed record UpdateInfo(Version Version, string Url, long Size, string Sha256);

/// <summary>Partie pure (sans réseau ni disque) : signature du manifeste, contraintes sur son contenu.</summary>
public static class UpdateVerifier
{
    /// <summary>
    /// A REMPLIR PAR MOMO : clé PUBLIQUE ECDSA P-256 (X||Y, 64 octets, base64), générée avec scripts/new-update-key.ps1.
    /// La clé privée va dans le secret CI FORGEOPTI_UPDATE_KEY, jamais dans le dépôt. Vide = toute mise à jour est refusée.
    /// </summary>
    public const string PublicKey = "";

    public const string ManifestUrl = "https://github.com/Heiphaistos/ForgeOpti/releases/latest/download/latest.json";
    public const string ChannelPrefix = "https://github.com/Heiphaistos/ForgeOpti/releases/download/";
    public const long MaxZipBytes = 300L * 1024 * 1024;

    public static UpdateInfo Parse(byte[] manifest, byte[] signatureBase64, string publicKey = PublicKey)
    {
        if (string.IsNullOrWhiteSpace(publicKey))
            throw new InvalidOperationException("Clé de signature des mises à jour non configurée : mise à jour refusée.");
        if (!VerifySignature(manifest, signatureBase64, publicKey))
            throw new InvalidDataException("Signature du manifeste de mise à jour invalide : mise à jour refusée.");
        UpdateInfo info;
        try
        {
            using var doc = JsonDocument.Parse(manifest);
            var r = doc.RootElement;
            info = new UpdateInfo(Version.Parse(r.GetProperty("version").GetString()!.TrimStart('v')), r.GetProperty("url").GetString() ?? "",
                r.GetProperty("size").GetInt64(), (r.GetProperty("sha256").GetString() ?? "").ToLowerInvariant());
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Manifeste de mise à jour illisible.", e);
        }
        if (!IsAllowedUrl(info.Url)) throw new InvalidDataException("Adresse de téléchargement hors du canal officiel.");
        if (info.Size is <= 0 or > MaxZipBytes) throw new InvalidDataException("Taille annoncée invalide.");
        if (info.Sha256.Length != 64 || !info.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("Empreinte SHA-256 invalide dans le manifeste.");
        return info;
    }

    public static bool VerifySignature(byte[] data, byte[] signatureBase64, string publicKey)
    {
        try
        {
            var xy = Convert.FromBase64String(publicKey.Trim());
            var sig = Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(signatureBase64).Trim());
            using var ecdsa = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = xy[..32], Y = xy[32..] } });
            return ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256);
        }
        catch (Exception e) when (e is FormatException or CryptographicException or ArgumentException) { return false; }
    }

    public static bool IsAllowedUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Scheme == Uri.UriSchemeHttps && u.UserInfo.Length == 0 && u.Query.Length == 0 && u.Fragment.Length == 0 && u.IsDefaultPort
        && u.AbsoluteUri.StartsWith(ChannelPrefix, StringComparison.Ordinal);

    /// <summary>Script PowerShell détaché. Apostrophes doublées (chemins) ; le zip est re-hashé APRÈS la fermeture de l'appli, juste avant extraction et copie.</summary>
    public static string BuildScript(int pid, string zip, string sha256, string stage, string dir, string exe, string work)
    {
        static string Q(string s) => "'" + s.Replace("'", "''") + "'";
        return $"Wait-Process -Id {pid} -ErrorAction SilentlyContinue; " +
               $"if ((Get-FileHash -Algorithm SHA256 -LiteralPath {Q(zip)}).Hash.ToLower() -ne {Q(sha256)}) {{ exit 1 }}; " +
               $"Expand-Archive -LiteralPath {Q(zip)} -DestinationPath {Q(stage)} -Force; " +
               $"Copy-Item -Path {Q(stage + "\\*")} -Destination {Q(dir)} -Recurse -Force; " +
               $"Start-Process {Q(exe)}; Remove-Item -LiteralPath {Q(work)} -Recurse -Force";
    }
}
