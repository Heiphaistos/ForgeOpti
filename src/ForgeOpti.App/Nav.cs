using ForgeOpti.Core;

namespace ForgeOpti.App;

/// <summary>Une entrée de navigation. Key : "home", "gaming-all", "clean" ou le nom d'une catégorie du catalogue.</summary>
public sealed record NavEntry(string Key, string Label, string Glyph);

public sealed record NavGroup(string Title, List<NavEntry> Entries);

/// <summary>Regroupement lisible des catégories du catalogue en grandes sections.</summary>
static class Nav
{
    public const string Home = "home", GamingAll = "gaming-all", Clean = "clean", Health = "health", Search = "search";

    static readonly (string Group, string[] Cats)[] Layout =
    [
        ("Performance et jeux", ["Gaming", "GPU et jeux", "Performance", "Alimentation", "Réseau", "Démarrage"]),
        ("Confidentialité et sécurité", ["Confidentialité", "Télémétrie", "IA et Copilot", "Sécurité", "Mises à jour"]),
        ("Interface", ["Interface", "Explorateur", "Menu contextuel", "Windows 10"]),
        ("Applications", ["Debloat apps", "Edge et OneDrive"]),
        ("Système avancé", ["Services", "Tâches planifiées", "Fonctionnalités Windows"]),
        ("Maintenance", ["Nettoyage"]),
    ];

    static readonly Dictionary<string, string> Glyphs = new()
    {
        ["Gaming"] = "", ["GPU et jeux"] = "", ["Performance"] = "", ["Alimentation"] = "", ["Réseau"] = "", ["Démarrage"] = "",
        ["Confidentialité"] = "", ["Télémétrie"] = "", ["IA et Copilot"] = "", ["Sécurité"] = "", ["Mises à jour"] = "",
        ["Interface"] = "", ["Explorateur"] = "", ["Menu contextuel"] = "", ["Windows 10"] = "",
        ["Debloat apps"] = "", ["Edge et OneDrive"] = "",
        ["Services"] = "", ["Tâches planifiées"] = "", ["Fonctionnalités Windows"] = "",
    };

    public static string GlyphFor(string cat) => Glyphs.TryGetValue(cat, out var g) ? g : "";

    /// <summary>Groupes de navigation à partir des catégories réellement présentes ; toute catégorie inconnue va dans « Autres ».</summary>
    public static List<NavGroup> Groups(IEnumerable<Tweak> tweaks)
    {
        var present = tweaks.Select(t => t.Category).Distinct().ToHashSet();
        var res = new List<NavGroup>();
        var used = new HashSet<string>();
        foreach (var (g, cats) in Layout)
        {
            var entries = cats.Where(present.Contains).Select(c => new NavEntry(c, c, GlyphFor(c))).ToList();
            foreach (var c in entries) used.Add(c.Key);
            if (g == "Performance et jeux") entries.Insert(0, new NavEntry(GamingAll, "★ Tous les réglages gaming", ""));
            if (g == "Maintenance") { entries.Insert(0, new NavEntry(Clean, "Nettoyage du disque", "")); entries.Insert(1, new NavEntry(Health, "Santé et persistance", "")); }
            if (entries.Count > 0) res.Add(new NavGroup(g, entries));
        }
        var others = present.Except(used).Order().Select(c => new NavEntry(c, c, GlyphFor(c))).ToList();
        if (others.Count > 0) res.Insert(Math.Max(0, res.Count - 1), new NavGroup("Autres", others)); // avant « Maintenance »
        return res;
    }
}
