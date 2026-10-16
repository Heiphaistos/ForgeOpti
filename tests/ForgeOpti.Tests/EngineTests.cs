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
}
