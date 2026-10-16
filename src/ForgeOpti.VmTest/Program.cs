using System.Text.Json;
using ForgeOpti.Core;

// Banc d'essai en phases pour VM jetable, lancé en session utilisateur élevée (ou SYSTEM).
// phase 0 : état de référence + application de TOUS les tweaks  -> redémarrage
// phase 1 : santé de Windows avec tout appliqué + persistance   -> annulation de tout + comparaison -> redémarrage
// phase 2 : santé finale, comparaison à l'état de référence     -> terminé
var dir = args.Length > 0 ? args[0] : @"C:\ForgeOptiTest";
Directory.CreateDirectory(dir);
var phaseFile = Path.Combine(dir, "phase.txt");
var phase = File.Exists(phaseFile) ? File.ReadAllText(phaseFile).Trim() : "0";
var all = Catalog.LoadAll().Where(t => t.MinBuild <= Environment.OSVersion.Version.Build
    && (t.MaxBuild == 0 || Environment.OSVersion.Version.Build <= t.MaxBuild)).ToList();
var engine = new Engine(Path.Combine(dir, "state.json"));
var J = new JsonSerializerOptions { WriteIndented = true };

void Log(string s) { Console.WriteLine(s); File.AppendAllText(Path.Combine(dir, "run.log"), $"[{DateTime.Now:HH:mm:ss}] [phase {phase}] {s}\n"); }
void Next(string p) => File.WriteAllText(phaseFile, p);
Dictionary<string, string?> Probes() => all.SelectMany(t => t.Ops.Select((o, i) => ($"{t.Id}#{i}", Engine.Probe(o)))).ToDictionary(x => x.Item1, x => x.Item2);
void Reboot() { Log("redémarrage…"); Engine.Ps("shutdown /r /t 10 /f"); Thread.Sleep(60000); }
string Safe(string ps) { try { return Engine.Ps(ps).Trim(); } catch (Exception e) { return "ERR " + e.Message; } }

void Health(string tag)
{
    Log($"santé {tag} : attente 90 s de stabilisation");
    Thread.Sleep(90000);
    var h = new Dictionary<string, string>
    {
        ["explorer"] = Safe("(Get-Process explorer -EA 0 | Measure-Object).Count"),
        ["internet"] = Safe("try{(Invoke-WebRequest https://www.msftconnecttest.com/connecttest.txt -UseBasicParsing -TimeoutSec 15).StatusCode}catch{'KO '+$_.Exception.Message}"),
        ["services_auto_arretes"] = Safe("(Get-Service | ? {$_.StartType -eq 'Automatic' -and $_.Status -ne 'Running'} | % Name | Sort-Object) -join ','"),
        ["services_cles"] = Safe("Get-Service EventLog,Dnscache,Audiosrv,BITS,WinDefend,wuauserv,Winmgmt,Themes -EA 0 | % { $_.Name+'='+$_.Status+'/'+$_.StartType }"),
        ["erreurs_evenements"] = Safe("$b=(Get-CimInstance Win32_OperatingSystem).LastBootUpTime; Get-WinEvent -FilterHashtable @{LogName='System','Application';Level=1,2;StartTime=$b} -EA 0 | Group-Object ProviderName | Sort-Object Count -Descending | Select-Object -First 15 | % { \"$($_.Count) $($_.Name)\" }"),
        ["crash_dump"] = Safe("(Test-Path C:\\Windows\\MEMORY.DMP).ToString()+' '+(Get-ChildItem C:\\Windows\\Minidump -EA 0 | Measure-Object).Count"),
        ["dism_checkhealth"] = Safe("DISM /Online /Cleanup-Image /CheckHealth | Select-Object -Last 4"),
        ["sfc_verifyonly"] = Safe("$o = & sfc /verifyonly; $o | Select-Object -Last 4"),
    };
    File.WriteAllText(Path.Combine(dir, $"health-{tag}.json"), JsonSerializer.Serialize(h, J));
    Log($"santé {tag} écrite");
}

Log($"démarrage, phase {phase}, {all.Count} tweaks, build {Environment.OSVersion.Version.Build}");
switch (phase)
{
    case "0":
        File.WriteAllText(Path.Combine(dir, "baseline-probes.json"), JsonSerializer.Serialize(Probes(), J));
        File.WriteAllText(Path.Combine(dir, "baseline-appx.txt"), Safe("Get-AppxPackage -AllUsers | % Name | Sort-Object -Unique | Out-String"));
        Health("baseline");
        try { RestorePoint.Create("ForgeOpti test"); Log("point de restauration OK"); } catch (Exception e) { Log("point de restauration KO : " + e.Message); }
        var res = new List<TestResult>();
        foreach (var t in all)
        {
            try { engine.Apply(t); res.Add(new(t.Id, "OK", "")); Log("applique " + t.Id); }
            catch (Exception e) { res.Add(new(t.Id, "ERROR", e.Message.Length > 300 ? e.Message[..300] : e.Message)); Log("ECHEC apply " + t.Id); }
        }
        File.WriteAllText(Path.Combine(dir, "apply-all.json"), JsonSerializer.Serialize(res, J));
        Next("1"); Reboot(); break;

    case "1":
        Health("tout-applique");
        var applied = Probes();
        File.WriteAllText(Path.Combine(dir, "probes-applied.json"), JsonSerializer.Serialize(applied, J));
        var rev = new List<TestResult>();
        foreach (var t in all)
        {
            try { engine.Revert(t); rev.Add(new(t.Id, "OK", "")); Log("annule " + t.Id); }
            catch (Exception e) { rev.Add(new(t.Id, "ERROR", e.Message.Length > 300 ? e.Message[..300] : e.Message)); Log("ECHEC revert " + t.Id); }
        }
        File.WriteAllText(Path.Combine(dir, "revert-all.json"), JsonSerializer.Serialize(rev, J));
        Next("2"); Reboot(); break;

    case "2":
        Health("apres-annulation");
        var baseP = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(Path.Combine(dir, "baseline-probes.json")))!;
        var now = Probes();
        var diff = baseP.Where(kv => now[kv.Key] != kv.Value).Select(kv => $"{kv.Key}: avant={kv.Value} apres={now[kv.Key]}").ToList();
        File.WriteAllLines(Path.Combine(dir, "diff-vs-baseline.txt"), diff);
        var a0 = File.ReadAllLines(Path.Combine(dir, "baseline-appx.txt")).Select(s => s.Trim()).Where(s => s.Length > 0).ToHashSet();
        var a1 = Safe("Get-AppxPackage -AllUsers | % Name | Sort-Object -Unique | Out-String").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToHashSet();
        File.WriteAllLines(Path.Combine(dir, "appx-manquantes.txt"), a0.Except(a1));
        Log($"FINI : {diff.Count} écarts registre/tâches, {a0.Except(a1).Count()} apps non réinstallées");
        Next("done"); break;

    default: Log("rien à faire"); break;
}
File.WriteAllText(Path.Combine(dir, $"phase{phase}.done"), "ok");
