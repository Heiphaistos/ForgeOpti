using System.Diagnostics;

namespace ForgeOpti.Core;

/// <summary>Persistance : réapplication des réglages défaits par Windows Update, et tâche planifiée qui la lance à l'ouverture de session.</summary>
public static class Maintenance
{
    const string TaskName = "ForgeOpti-Verification";

    public static bool AutoRepair
    {
        get => Settings.Get("autoRepair", true);
        set => Settings.Set("autoRepair", value);
    }

    public static string StatePath => Path.Combine(Log.Dir, "state.json");

    /// <summary>Vérifie tous les réglages appliqués et réécrit ceux qui ont dérivé. Journalise le résultat.</summary>
    public static List<RepairItem> Repair(Engine engine, IEnumerable<Tweak> catalog)
    {
        var res = engine.Repair(catalog);
        if (res.Count == 0) Log.Info("Vérification : tous les réglages appliqués sont intacts.");
        foreach (var r in res)
            if (r.Error is null) Log.Warn($"Réappliqué (défait par Windows ?) : {r.Id} ({r.Ops} valeur(s))");
            else Log.Error($"Réapplication impossible : {r.Id} : {r.Error}");
        Settings.Set("lastCheck", DateTime.Now.ToString("s"));
        return res;
    }

    /// <summary>Crée (ou met à jour le chemin de) la tâche d'ouverture de session, ou la supprime.</summary>
    public static void SyncTask(bool enable)
    {
        try
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("chemin de l'exécutable inconnu");
            var args = enable
                ? $"/Create /TN {TaskName} /TR \"\\\"{exe}\\\" --repair\" /SC ONLOGON /RL HIGHEST /F"
                : $"/Delete /TN {TaskName} /F";
            var psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            var err = p.StandardError.ReadToEndAsync();
            p.StandardOutput.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0 && enable) throw new InvalidOperationException(err.Result.Trim());
        }
        catch (Exception e) { Log.Error("Tâche de vérification : " + e.Message); }
    }
}
