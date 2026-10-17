using ForgeOpti.Core;

/// <summary>Modes ponctuels du banc d'essai : "clean" (module de nettoyage) et "only id1,id2" (apply+revert ciblés).</summary>
static class Modes
{
    public static bool Run(string dir, string[] args, Engine engine, Func<string, string> safe)
    {
        var mode = args.Length > 1 ? args[1] : "";
        if (mode == "clean") { Clean(dir, safe); return true; }
        if (mode == "only") { Only(dir, args[2].Split(','), engine, safe); return true; }
        return false;
    }

    static void Clean(string dir, Func<string, string> safe)
    {
        var junk = Path.Combine(Path.GetTempPath(), "fo-junk");
        Directory.CreateDirectory(junk);
        File.WriteAllBytes(Path.Combine(junk, "j.bin"), new byte[20_000_000]);
        var rep = new List<string>();
        foreach (var it in Cleaner.Items)
        {
            if (it.Id == "composants") { rep.Add("SKIP composants (DISM StartComponentCleanup : plusieurs minutes dans la VM)"); continue; }
            try
            {
                var before = Cleaner.Measure(it);
                var freed = Cleaner.Clean(it);
                rep.Add($"OK {it.Id} avant={before} libere={freed} apres={Cleaner.Measure(it)}");
            }
            catch (Exception e) { rep.Add($"ERREUR {it.Id} {e.Message}"); }
        }
        rep.Add("explorer=" + safe("(Get-Process explorer -EA 0 | Measure-Object).Count"));
        File.WriteAllLines(Path.Combine(dir, "clean-report.txt"), rep);
    }

    static string Flat(Exception e) => e.Message.Replace("\r", " ").Replace("\n", " ");

    static void Only(string dir, string[] ids, Engine engine, Func<string, string> safe)
    {
        var rep = new List<string>();
        foreach (var t in Catalog.LoadAll().Where(t => ids.Contains(t.Id)))
        {
            var before = t.Ops.Select(Engine.Probe).ToList();
            try { engine.Apply(t); rep.Add($"apply OK {t.Id}"); } catch (Exception e) { rep.Add($"apply KO {t.Id}: {Flat(e)}"); }
            try { engine.Revert(t); rep.Add($"revert OK {t.Id}"); } catch (Exception e) { rep.Add($"revert KO {t.Id}: {Flat(e)}"); }
            rep.Add($"  etat identique: {before.SequenceEqual(t.Ops.Select(Engine.Probe))}");
        }
        rep.Add("appx WebExperience: " + safe("(Get-AppxPackage -AllUsers -Name MicrosoftWindows.Client.WebExperience | Measure-Object).Count"));
        rep.Add("appx Solitaire: " + safe("(Get-AppxPackage -AllUsers -Name Microsoft.MicrosoftSolitaireCollection | Measure-Object).Count"));
        File.WriteAllLines(Path.Combine(dir, "only-report.txt"), rep);
    }
}
