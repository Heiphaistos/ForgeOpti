using System.Security.Cryptography;
using System.Text;
using ForgeOpti.Core;
using Xunit;

public class UpdateVerifierTests
{
    static readonly string Good = UpdateVerifier.ChannelPrefix + "v9.9.9/ForgeOpti-win-x64.zip";
    static readonly string Sha = new('a', 64);

    // Clé de TEST jetable, générée à l'exécution : aucune clé réelle dans le dépôt.
    static (string pub, ECDsa key) NewKey()
    {
        var k = ECDsa.Create(ECCurve.NamedCurves.nistP256); var p = k.ExportParameters(false);
        return (Convert.ToBase64String([.. p.Q.X!, .. p.Q.Y!]), k);
    }
    static byte[] Sign(ECDsa k, byte[] m) => Encoding.ASCII.GetBytes(Convert.ToBase64String(k.SignData(m, HashAlgorithmName.SHA256)));
    static byte[] Man(string url, string? sha = null, long size = 1000) =>
        Encoding.UTF8.GetBytes($$"""{"version":"9.9.9","url":"{{url}}","size":{{size}},"sha256":"{{sha ?? Sha}}"}""");

    [Fact]
    public void Manifeste_signe_valide_est_accepte()
    {
        var (pub, k) = NewKey(); var m = Man(Good);
        Assert.Equal(new Version(9, 9, 9), UpdateVerifier.Parse(m, Sign(k, m), pub).Version);
    }

    [Fact]
    public void Manifeste_modifie_ou_autre_cle_est_refuse()
    {
        var (pub, k) = NewKey(); var m = Man(Good); var sig = Sign(k, m);
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Parse(Man(Good, new string('b', 64)), sig, pub));
        var (pub2, _) = NewKey();
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Parse(m, sig, pub2));
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Parse(m, Encoding.ASCII.GetBytes("pas une signature"), pub));
    }

    [Fact]
    public void Cle_non_configuree_refuse_toute_mise_a_jour()
    {
        Assert.Equal("", UpdateVerifier.PublicKey);
        var (_, k) = NewKey(); var m = Man(Good);
        Assert.Throws<InvalidOperationException>(() => UpdateVerifier.Parse(m, Sign(k, m)));
    }

    [Theory]
    [InlineData("https://evil.example/ForgeOpti-win-x64.zip")]
    [InlineData("http://github.com/Heiphaistos/ForgeOpti/releases/download/v1/x.zip")]
    [InlineData("https://github.com/Heiphaistos/ForgeOpti/releases/download/v1/x.zip?a=b")]
    [InlineData("https://user@github.com/Heiphaistos/ForgeOpti/releases/download/v1/x.zip")]
    [InlineData("https://github.com/Heiphaistos/ForgeOpti/releases/download/../../../evil/x.zip")]
    [InlineData("https://github.com/Heiphaistos/Autre/releases/download/v1/x.zip")]
    public void Url_hors_canal_refusee(string url)
    {
        Assert.False(UpdateVerifier.IsAllowedUrl(url));
        var (pub, k) = NewKey(); var m = Man(url);
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Parse(m, Sign(k, m), pub));
    }

    [Fact]
    public void Taille_et_empreinte_invalides_refusees()
    {
        var (pub, k) = NewKey();
        var m1 = Man(Good, size: 0); var m2 = Man(Good, sha: "zz");
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Parse(m1, Sign(k, m1), pub));
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Parse(m2, Sign(k, m2), pub));
    }

    [Fact]
    public void Script_double_les_apostrophes_et_reverifie_le_hash_avant_la_copie()
    {
        var s = UpdateVerifier.BuildScript(42, @"C:\PD\o'brien\a.zip", Sha, @"C:\PD\o'brien\new", @"C:\Program Files\O'Neil", @"C:\Program Files\O'Neil\x.exe", @"C:\PD\o'brien");
        Assert.Contains(@"'C:\PD\o''brien\a.zip'", s);
        Assert.Contains(@"'C:\Program Files\O''Neil'", s);
        Assert.True(s.IndexOf("Get-FileHash") < s.IndexOf("Expand-Archive") && s.IndexOf("Expand-Archive") < s.IndexOf("Copy-Item"));
    }
}
