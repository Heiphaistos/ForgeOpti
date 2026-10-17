using System.Text.Json;
using ForgeOpti.Core;
using Microsoft.Win32;
using Xunit;

public class EngineTests
{
    const string Key = @"Software\ForgeOptiTest";

    static Tweak T(params Op[] ops) => new("t.test", "Test", "t", "d", Risk.Sur, false, false, false, [.. ops]);
    static RegOp R(string name, int v) => new("HKCU", Key, name, "DWord", JsonSerializer.SerializeToElement(v));

    [Fact]
    public void Apply_puis_Revert_restaure_valeur_existante_et_supprime_valeur_absente()
    {
        var state = Path.Combine(Path.GetTempPath(), $"fo-{Guid.NewGuid()}.json");
        using (var k = Registry.CurrentUser.CreateSubKey(Key)) { k.SetValue("A", 5, RegistryValueKind.DWord); k.DeleteValue("B", false); }
        try
        {
            var e = new Engine(state); var t = T(R("A", 0), R("B", 1));
            e.Apply(t);
            using (var k = Registry.CurrentUser.OpenSubKey(Key)!) { Assert.Equal(0, k.GetValue("A")); Assert.Equal(1, k.GetValue("B")); }
            Assert.True(new Engine(state).IsApplied("t.test")); // survit au redémarrage de l'appli
            new Engine(state).Revert(t);
            using (var k = Registry.CurrentUser.OpenSubKey(Key)!) { Assert.Equal(5, k.GetValue("A")); Assert.Null(k.GetValue("B")); }
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(Key, false); File.Delete(state); }
    }

    [Fact]
    public void CmdOp_sans_Revert_refuse()
    {
        var e = new Engine(Path.Combine(Path.GetTempPath(), $"fo-{Guid.NewGuid()}.json"));
        Assert.Throws<InvalidOperationException>(() => e.Apply(T(new CmdOp("echo", ""))));
    }

    [Fact]
    public void Catalogue_se_charge_sans_doublon() => Assert.NotEmpty(Catalog.LoadAll());

    [Fact]
    public void Catalogue_est_coherent()
    {
        foreach (var t in Catalog.LoadAll())
        {
            Assert.NotEmpty(t.Ops);
            if (t.Risk == Risk.Avance) Assert.False(t.OneClick, t.Id);
            foreach (var o in t.Ops)
            {
                if (o is CmdOp c) { Assert.False(string.IsNullOrWhiteSpace(c.Revert), t.Id); Assert.DoesNotContain(c.Apply + c.Revert, ch => char.IsControl(ch) && ch != (char)10 && ch != (char)13 && ch != (char)9); }
                if (o is RegOp r) { Assert.Contains(r.Hive, new[] { "HKLM", "HKCU" }); Assert.Contains(r.Kind, new[] { "DWord", "QWord", "String", "ExpandString" }); }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Tweaks_qui_se_chevauchent_restaurent_l_original_quel_que_soit_l_ordre(bool revertSecondFirst)
    {
        var state = Path.Combine(Path.GetTempPath(), $"fo-{Guid.NewGuid()}.json");
        using (var k = Registry.CurrentUser.CreateSubKey(Key)) { k.SetValue("C", 7, RegistryValueKind.DWord); k.DeleteValue("D", false); }
        try
        {
            var e = new Engine(state);
            var a = T(R("C", 0), R("D", 1)) with { Id = "t.a" };
            var b = T(R("C", 1)) with { Id = "t.b" };
            e.Apply(a); e.Apply(b);
            if (revertSecondFirst) { e.Revert(b); e.Revert(a); } else { e.Revert(a); e.Revert(b); }
            using var k2 = Registry.CurrentUser.OpenSubKey(Key)!;
            Assert.Equal(7, k2.GetValue("C"));
            Assert.Null(k2.GetValue("D"));
        }
        finally { Registry.CurrentUser.DeleteSubKeyTree(Key, false); File.Delete(state); }
    }

    [Fact]
    public void Revert_dont_la_commande_echoue_garde_le_tweak_applique_pour_reessayer()
    {
        var e = new Engine(Path.Combine(Path.GetTempPath(), $"fo-{Guid.NewGuid()}.json"));
        var t = T(new CmdOp("exit 0", "exit 3")) with { Id = "t.cmd" };
        e.Apply(t);
        Assert.Throws<InvalidOperationException>(() => e.Revert(t));
        Assert.True(e.IsApplied("t.cmd"));
    }

    [Fact]
    public void Cleaner_supprime_les_fichiers_et_rapporte_la_taille()
    {
        var d = Path.Combine(Path.GetTempPath(), $"fo-clean-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(d, "sub"));
        File.WriteAllBytes(Path.Combine(d, "a.tmp"), new byte[1000]);
        File.WriteAllBytes(Path.Combine(d, "sub", "b.tmp"), new byte[500]);
        var it = new CleanItem("t", "t", "t", () => [d]);
        Assert.Equal(1500, Cleaner.Measure(it));
        Assert.Equal(1500, Cleaner.Clean(it));
        Assert.Equal(0, Cleaner.Measure(it));
        Assert.False(Directory.Exists(Path.Combine(d, "sub")));
        Directory.Delete(d, true);
    }
}
