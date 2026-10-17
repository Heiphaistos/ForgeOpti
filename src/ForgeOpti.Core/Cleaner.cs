namespace ForgeOpti.Core;

/// <summary>Élément de nettoyage. Irréversible par nature : ne touche que des caches/temporaires régénérés par Windows.</summary>
public sealed record CleanItem(string Id, string Name, string Description, Func<IEnumerable<string>> Dirs, string? Pattern = null, Action? Special = null);

public static class Cleaner
{
    static string Env(string n) => Environment.GetEnvironmentVariable(n) ?? "";
    static string Win => Env("SystemRoot");
    static string Local => Env("LOCALAPPDATA");
    static string PData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    static IEnumerable<string> BrowserCaches(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).SelectMany(p => new[] { "Cache", "Code Cache", "GPUCache" }.Select(c => Path.Combine(p, c)))
            : [];

    public static IReadOnlyList<CleanItem> Items { get; } =
    [
        new("temp-user", "Fichiers temporaires (utilisateur)", "Dossier %TEMP% de la session.", () => [Path.GetTempPath()]),
        new("temp-windows", "Fichiers temporaires (Windows)", "C:\\Windows\\Temp.", () => [Path.Combine(Win, "Temp")]),
        new("maj-cache", "Cache de Windows Update", "Paquets déjà installés dans SoftwareDistribution\\Download.", () => [Path.Combine(Win, "SoftwareDistribution", "Download")]),
        new("livraison", "Cache d'optimisation de la distribution", "Fichiers partagés en pair-à-pair par Windows.", () => [Path.Combine(Win, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache")]),
        new("miniatures", "Cache des miniatures et icônes", "Reconstruit automatiquement par l'Explorateur.", () => [Path.Combine(Local, "Microsoft", "Windows", "Explorer")], "*cache*.db"),
        new("rapports-erreurs", "Rapports d'erreurs et vidages mémoire", "WER, CrashDumps, Minidump.", () => [Path.Combine(PData, "Microsoft", "Windows", "WER", "ReportArchive"), Path.Combine(PData, "Microsoft", "Windows", "WER", "ReportQueue"), Path.Combine(Local, "CrashDumps"), Path.Combine(Win, "Minidump")]),
        new("cache-edge", "Cache de Microsoft Edge", "Cache, Code Cache, GPUCache (les mots de passe et cookies ne sont pas touchés).", () => BrowserCaches(Path.Combine(Local, "Microsoft", "Edge", "User Data"))),
        new("cache-chrome", "Cache de Google Chrome", "Cache, Code Cache, GPUCache (les mots de passe et cookies ne sont pas touchés).", () => BrowserCaches(Path.Combine(Local, "Google", "Chrome", "User Data"))),
        new("corbeille", "Corbeille", "Vide la corbeille de tous les disques.", () => [], null, () => Engine.Ps("Clear-RecycleBin -Force -ErrorAction SilentlyContinue")),
        new("dns", "Cache DNS", "Vide le cache de résolution de noms.", () => [], null, () => Engine.Ps("ipconfig /flushdns | Out-Null")),
        new("composants", "Composants Windows obsolètes (WinSxS)", "DISM /StartComponentCleanup : long, sans risque.", () => [], null, () => Engine.Ps("Dism.exe /Online /Cleanup-Image /StartComponentCleanup | Out-Null")),
    ];

    static IEnumerable<string> Files(CleanItem it) =>
        it.Dirs().Where(Directory.Exists).SelectMany(d =>
        {
            try { return Directory.EnumerateFiles(d, it.Pattern ?? "*", it.Pattern is null ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly); }
            catch { return []; }
        });

    public static long Measure(CleanItem it)
    {
        long n = 0;
        foreach (var f in Files(it)) { try { n += new FileInfo(f).Length; } catch { /* fichier disparu */ } }
        return n;
    }

    /// <summary>Supprime au mieux (les fichiers verrouillés sont ignorés). Retourne les octets libérés.</summary>
    public static long Clean(CleanItem it)
    {
        long freed = 0;
        foreach (var f in Files(it).ToList())
        {
            try { var len = new FileInfo(f).Length; File.Delete(f); freed += len; } catch { /* verrouillé : ignoré */ }
        }
        if (it.Pattern is null)
            foreach (var d in it.Dirs().Where(Directory.Exists))
                foreach (var sub in SafeDirs(d)) { try { Directory.Delete(sub, false); } catch { /* non vide ou verrouillé */ } }
        it.Special?.Invoke();
        return freed;
    }

    static IEnumerable<string> SafeDirs(string d)
    {
        try { return Directory.EnumerateDirectories(d, "*", SearchOption.AllDirectories).OrderByDescending(x => x.Length).ToList(); }
        catch { return []; }
    }
}
