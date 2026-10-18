using System.Text.Json;
using Microsoft.Win32;

namespace ForgeOpti.Core;

public sealed record TestResult(string Id, string Status, string Detail);

/// <summary>Banc d'essai pour VM jetable : applique chaque tweak, vérifie l'effet, annule, vérifie le retour exact.</summary>
public static class SelfTest
{
    public static List<TestResult> Run(IEnumerable<Tweak> tweaks, string reportFile, Action<string>? progress = null)
    {
        var state = Path.Combine(Path.GetTempPath(), $"fo-selftest-{Guid.NewGuid()}.json");
        var engine = new Engine(state);
        var results = new List<TestResult>();
        var build = Environment.OSVersion.Version.Build;

        void Add(TestResult r)
        {
            results.Add(r);
            File.WriteAllText(reportFile, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            progress?.Invoke($"{r.Status} {r.Id} {r.Detail}");
        }

        try { RestorePoint.Create("ForgeOpti selftest"); Add(new("restore-point", "OK", "")); }
        catch (Exception e) { Add(new("restore-point", "FAIL", e.Message)); }

        foreach (var t in tweaks.Where(t => t.MinBuild <= build && (t.MaxBuild == 0 || build <= t.MaxBuild)))
        {
            var before = t.Ops.Select(Engine.Probe).ToList();
            try
            {
                engine.Apply(t);
                var applied = t.Ops.Select(Engine.Probe).ToList();
                var problems = new List<string>();
                for (var i = 0; i < t.Ops.Count; i++)
                    if (t.Ops[i] is RegOp r && applied[i] != Engine.Expected(r)) problems.Add($"op{i} apres Apply={applied[i]} attendu={Engine.Expected(r)}");
                engine.Revert(t);
                var after = t.Ops.Select(Engine.Probe).ToList();
                for (var i = 0; i < t.Ops.Count; i++)
                    if (after[i] != before[i]) problems.Add($"op{i} apres Revert={after[i]} avant={before[i]}");
                Add(problems.Count == 0 ? new(t.Id, "OK", "") : new(t.Id, "FAIL", string.Join(" | ", problems)));
            }
            catch (Exception e)
            {
                try { engine.Revert(t); } catch { /* meilleur effort */ }
                Add(new(t.Id, "ERROR", e.Message.Length > 300 ? e.Message[..300] : e.Message));
            }
        }
        File.Delete(state);
        return results;
    }

    /// <summary>Remonte un message à l'hôte Hyper-V via le canal KVP (clé parente à créer d'abord).</summary>
    public static void Kvp(string name, string value)
    {
        try
        {
            using var k = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Virtual Machine\Guest\Parameters");
            k.SetValue(name, value.Length > 900 ? value[..900] : value);
        }
        catch { /* hors VM Hyper-V : ignoré */ }
    }
}
