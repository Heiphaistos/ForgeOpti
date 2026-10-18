using System.Text.Json;
using System.Text.RegularExpressions;

namespace ForgeOpti.Core;

/// <summary>
/// Profils préconfigurés (présélection seulement : l'utilisateur ajuste puis applique) et réglages « vitaux » qu'aucun profil ni le 1 clic ne touche :
/// impression, Bluetooth, découverte réseau/partages, mises à jour et protection de sécurité de Windows.
/// </summary>
public static partial class Profiles
{
    public const string Gaming = "Gaming", Bureautique = "Bureautique", Hybride = "Hybride", Confidentialite = "Confidentialité";
    public static readonly string[] Builtin = [Gaming, Bureautique, Hybride, Confidentialite];

    static readonly HashSet<string> ProtectedIds =
    [
        // impression, fax, numérisation
        "svc.spooler", "feat.printing-xpsservices-features", "feat.printing-foundation-internetprinting-client", "feat.printing-foundation-lprportmonitor",
        "feat.cap-faxscan", "feat.cap-xpsviewer",
        // Bluetooth et périphériques radio
        "svc.bluetooth", "revi.permissions-peripheriques-refuses", "ws.applis-refuser-radios",
        // découverte réseau et partages
        "svc.ssdp-upnp", "svc.lltd", "atlas.svc-decouverte-reseau", "atlas.anonyme-partages",
        // mises à jour et protection de Windows
        "ws.maj-pause-etendue", "ws.maj-fonctionnalites-differees", "ws.maj-connexion-limitee", "maj.differer-fonctionnalites", "maj.differer-qualite-7j",
        "maj.notifier-avant-telechargement", "maj.msrt-exclu", "secu.defender-analyse-planifiee-off",
    ];

    // Filet de sécurité : tout réglage qui écrit dans l'un de ces services (clé de registre) ou les cite dans une commande est protégé.
    [GeneratedRegex(@"\b(spooler|printnotify|printworkflowusersvc|bthserv|bthavctpsvc|btagservice|bluetoothusersvc|ssdpsrv|upnphost|fdrespub|fdphost|lanmanserver|lanmanworkstation|wuauserv|usosvc|waasmedicsvc|windefend|wscsvc|mpssvc|bfe)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VitalService();

    public static bool Protected(Tweak t) =>
        ProtectedIds.Contains(t.Id) || t.Ops.Any(o => o switch
        {
            RegOp r => r.Key.Contains(@"\Services\", StringComparison.OrdinalIgnoreCase) && VitalService().IsMatch(r.Key),
            CmdOp c => VitalService().IsMatch(c.Apply),
            _ => false,
        });

    /// <summary>Réglages d'un profil, protégés exclus, déjà actifs inclus (l'appelant ignore ceux qui sont appliqués).</summary>
    public static List<Tweak> Select(string profile, IEnumerable<Tweak> all)
    {
        var ok = all.Where(t => !Protected(t)).ToList();
        bool privacy(Tweak t) => t.Category is "Confidentialité" or "Télémétrie" or "IA et Copilot";
        var gaming = ok.Where(t => t.Gaming && t.Risk != Risk.Avance);
        var office = ok.Where(t => t.Risk == Risk.Sur && !t.Gaming && (t.OneClick || privacy(t)));
        return profile switch
        {
            Gaming => gaming.ToList(),
            Bureautique => office.ToList(),
            Hybride => office.Concat(gaming).Distinct().ToList(),
            Confidentialite => ok.Where(t => privacy(t) && t.Risk == Risk.Sur).ToList(),
            _ => [],
        };
    }

    /// <summary>Réglages du « 1 clic » : ceux marqués dans le catalogue, sans les réglages protégés.</summary>
    public static List<Tweak> OneClick(IEnumerable<Tweak> all) => all.Where(t => t.OneClick && !Protected(t)).ToList();

    // ---- profils personnels : nom -> identifiants des réglages ----
    static string CustomFile => Path.Combine(Log.Dir, "profiles.json");

    public static Dictionary<string, List<string>> LoadCustom()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, List<string>>>(File.ReadAllText(CustomFile)) ?? []; }
        catch { return []; }
    }

    public static void SaveCustom(string name, IEnumerable<string> ids)
    {
        var all = LoadCustom();
        all[name] = ids.Distinct().Order().ToList();
        Directory.CreateDirectory(Log.Dir);
        File.WriteAllText(CustomFile, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static void DeleteCustom(string name)
    {
        var all = LoadCustom();
        if (all.Remove(name)) File.WriteAllText(CustomFile, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Réglages d'un profil personnel encore présents au catalogue.</summary>
    public static List<Tweak> SelectCustom(string name, IEnumerable<Tweak> all) =>
        LoadCustom().TryGetValue(name, out var ids) ? all.Where(t => ids.Contains(t.Id)).ToList() : [];
}
