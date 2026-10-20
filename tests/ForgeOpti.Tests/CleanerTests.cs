using System.Diagnostics;
using ForgeOpti.Core;
using Xunit;

public class CleanerTests
{
    [Fact]
    public void Clean_ne_suit_pas_les_jonctions()
    {
        var root = Path.Combine(Path.GetTempPath(), "fo-clean-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, "cible"); var scan = Path.Combine(root, "scan");
        Directory.CreateDirectory(target); Directory.CreateDirectory(scan);
        File.WriteAllText(Path.Combine(target, "protege.txt"), "x");
        File.WriteAllText(Path.Combine(scan, "jetable.txt"), "x");
        var link = Path.Combine(scan, "lien");
        using (var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"") { CreateNoWindow = true })!) p.WaitForExit();
        Assert.True(Directory.Exists(link));
        try
        {
            var it = new CleanItem("t", "t", "t", () => [scan]);
            Cleaner.Clean(it);
            Assert.True(File.Exists(Path.Combine(target, "protege.txt")), "le fichier derrière la jonction a été supprimé");
            Assert.False(File.Exists(Path.Combine(scan, "jetable.txt")));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); Directory.Delete(root, true); }
    }
}
