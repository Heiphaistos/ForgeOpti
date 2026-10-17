namespace ForgeOpti.Core;

/// <summary>Éléments de nettoyage supplémentaires : uniquement caches, temporaires et journaux régénérables.</summary>
public static class CleanerExtra
{
    static string Env(string n) => Environment.GetEnvironmentVariable(n) ?? "";
    static string Win => Env("SystemRoot");
    static string Local => Env("LOCALAPPDATA");
    static string Roam => Env("APPDATA");
    static string PData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    static IEnumerable<string> Sub(string root, params string[] names) => names.Select(n => Path.Combine(root, n));

    // Profils Chromium : Cache / Code Cache / GPUCache dans chaque profil.
    static IEnumerable<string> Chromium(string root) =>
        Directory.Exists(root)
            ? Directory.EnumerateDirectories(root).SelectMany(p => Sub(p, "Cache", "Code Cache", "GPUCache"))
            : [];

    // Profils Firefox : cache2 sous %LOCALAPPDATA%\Mozilla\Firefox\Profiles\<profil>.
    static IEnumerable<string> Firefox() =>
        Directory.Exists(Path.Combine(Local, "Mozilla", "Firefox", "Profiles"))
            ? Directory.EnumerateDirectories(Path.Combine(Local, "Mozilla", "Firefox", "Profiles")).Select(p => Path.Combine(p, "cache2"))
            : [];

    // Applications Electron : Cache, Code Cache, GPUCache directement sous le dossier de données.
    static IEnumerable<string> Electron(string root) => Sub(root, "Cache", "Code Cache", "GPUCache", "DawnCache");

    public static IReadOnlyList<CleanItem> Items { get; } =
    [
        new("cache-firefox", "Cache de Mozilla Firefox", "cache2 de chaque profil (mots de passe, cookies et favoris intacts).", Firefox),
        new("cache-brave", "Cache de Brave", "Cache, Code Cache, GPUCache (profils conservés).", () => Chromium(Path.Combine(Local, "BraveSoftware", "Brave-Browser", "User Data"))),
        new("cache-opera", "Cache d'Opera", "Cache, Code Cache, GPUCache d'Opera et Opera GX.", () => Sub(Path.Combine(Local, "Opera Software"), "Opera Stable/Cache", "Opera Stable/Code Cache", "Opera Stable/GPUCache", "Opera GX Stable/Cache", "Opera GX Stable/Code Cache", "Opera GX Stable/GPUCache")),
        new("cache-vivaldi", "Cache de Vivaldi", "Cache, Code Cache, GPUCache (profils conservés).", () => Chromium(Path.Combine(Local, "Vivaldi", "User Data"))),
        new("cache-discord", "Cache de Discord", "Cache, Code Cache, GPUCache (la session reste connectée).", () => Electron(Path.Combine(Roam, "discord"))),
        new("cache-teams", "Cache de Microsoft Teams", "Cache de Teams classique et du nouveau Teams.", () => Electron(Path.Combine(Roam, "Microsoft", "Teams")).Concat(Sub(Local, "Packages/MSTeams_8wekyb3d8bbwe/LocalCache/Microsoft/MSTeams/EBWebView/Default/Cache"))),
        new("cache-spotify", "Cache de Spotify", "Cache web de l'interface (le dossier Data, qui contient les titres hors ligne, n'est pas touché).", () => [Path.Combine(Local, "Spotify", "Browser", "Cache")]),
        new("cache-steam", "Cache web de Steam", "htmlcache du client Steam (jeux et sauvegardes intacts).", () => [Path.Combine(Local, "Steam", "htmlcache")]),
        new("cache-epic", "Cache d'Epic Games Launcher", "webcache de la boutique (jeux installés intacts).", () => Directory.Exists(Path.Combine(Local, "EpicGamesLauncher", "Saved")) ? Directory.EnumerateDirectories(Path.Combine(Local, "EpicGamesLauncher", "Saved"), "webcache*") : []),
        new("shaders-nvidia", "Cache de shaders NVIDIA", "DXCache, GLCache et NV_Cache : recréés au prochain lancement des jeux (premier démarrage un peu plus lent).", () => [Path.Combine(Local, "NVIDIA", "DXCache"), Path.Combine(Local, "NVIDIA", "GLCache"), Path.Combine(PData, "NVIDIA Corporation", "NV_Cache")]),
        new("shaders-amd", "Cache de shaders AMD", "DxCache, GLCache et VkCache : recréés automatiquement.", () => [Path.Combine(Local, "AMD", "DxCache"), Path.Combine(Local, "AMD", "GLCache"), Path.Combine(Local, "AMD", "VkCache")]),
        new("shaders-directx", "Cache de shaders DirectX", "D3DSCache : recréé automatiquement.", () => [Path.Combine(Local, "D3DSCache")]),
        new("journaux-windows", "Journaux d'installation et de maintenance Windows", "Fichiers .log de CBS et DISM (journaux de diagnostic).", () => [Path.Combine(Win, "Logs", "CBS"), Path.Combine(Win, "Logs", "DISM")], "*.log"),
        new("prefetch", "Prefetch", "Fichiers .pf de préchargement ; Windows les reconstruit (premier lancement des applications un peu plus lent).", () => [Path.Combine(Win, "Prefetch")], "*.pf"),
        new("cache-polices", "Cache des polices", "FontCache : régénéré par Windows (fichiers .dat).", () => [Path.Combine(Win, "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache")], "*.dat"),
        new("rapports-wer-utilisateur", "Rapports d'erreurs de l'utilisateur", "Rapports WER archivés dans le profil.", () => [Path.Combine(Local, "Microsoft", "Windows", "WER", "ReportArchive"), Path.Combine(Local, "Microsoft", "Windows", "WER", "ReportQueue")]),
        new("cache-icones", "Cache des icônes", "IconCache.db : l'Explorateur le recrée (les icônes peuvent se redessiner une fois).", () => [Local], "IconCache.db"),
        new("recents-liste", "Documents récents", "Raccourcis du dossier Recent (les fichiers eux-mêmes ne sont pas supprimés).", () => [Path.Combine(Roam, "Microsoft", "Windows", "Recent")], "*.lnk"),
        new("jump-lists", "Listes de raccourcis de la barre des tâches", "Fichiers automatiques et personnalisés des Jump Lists (épingles personnalisées perdues).", () => [Path.Combine(Roam, "Microsoft", "Windows", "Recent", "AutomaticDestinations")], "*.automaticDestinations-ms"),
        new("cache-store", "Cache du Microsoft Store", "LocalCache du Store ; reconstruit à l'ouverture.", () => [Path.Combine(Local, "Packages", "Microsoft.WindowsStore_8wekyb3d8bbwe", "LocalCache")]),
        new("presse-papiers", "Presse-papiers", "Vide le contenu actuel du presse-papiers.", () => [], null, () => Engine.Ps("Set-Clipboard -Value $null")),
    ];
}
