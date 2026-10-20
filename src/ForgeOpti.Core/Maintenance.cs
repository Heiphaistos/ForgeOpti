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

    public const string PortableMessage = "Installe ForgeOpti avec l'installeur (Program Files) pour activer la vérification automatique : la version portable n'est pas dans un dossier protégé.";

    /// <summary>La tâche s'exécute en HIGHEST sans UAC : l'exe visé doit être dans un dossier que seul un admin peut modifier.</summary>
    public static bool IsProtectedPath(string exe)
    {
        var full = Path.GetFullPath(exe);
        return new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 }
            .Select(Environment.GetFolderPath).Where(r => r.Length > 0)
            .Any(r => full.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(r)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Crée (ou met à jour le chemin de) la tâche d'ouverture de session, ou la supprime. Retourne un message à afficher si elle est refusée.</summary>
    public static string? SyncTask(bool enable)
    {
        try
        {
            var exe = Environment.ProcessPath ?? throw new InvalidOperationException("chemin de l'exécutable inconnu");
            var refused = enable && !IsProtectedPath(exe);
            if (refused) Log.Warn("Tâche de vérification non créée : exécutable hors Program Files (" + exe + ")");
            var args = enable && !refused
                ? $"/Create /TN {TaskName} /TR \"\\\"{exe}\\\" --repair\" /SC ONLOGON /RL HIGHEST /F"
                : $"/Delete /TN {TaskName} /F"; // refus : on retire aussi une ancienne tâche pointant vers un chemin modifiable
            var psi = new ProcessStartInfo("schtasks.exe", args) { CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var p = Process.Start(psi)!;
            var err = p.StandardError.ReadToEndAsync();
            p.StandardOutput.ReadToEnd(); p.WaitForExit();
            if (p.ExitCode != 0 && enable && !refused) throw new InvalidOperationException(err.Result.Trim());
            return refused ? PortableMessage : null;
        }
        catch (Exception e) { Log.Error("Tâche de vérification : " + e.Message); return null; }
    }
}
