using System.Text.Json;
using System.Text.Json.Nodes;

namespace ForgeOpti.Core;

/// <summary>Réglages de l'application (apparence, mises à jour auto) : un seul fichier JSON dans ProgramData\ForgeOpti.</summary>
public static class Settings
{
    static string FilePath => Path.Combine(Log.Dir, "settings.json");

    static JsonObject Load()
    {
        try { return JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject ?? []; }
        catch { return []; }
    }

    public static T Get<T>(string key, T fallback)
    {
        try { return Load()[key] is { } n ? n.GetValue<T>() : fallback; }
        catch { return fallback; }
    }

    public static void Set<T>(string key, T value)
    {
        try
        {
            var o = Load();
            o[key] = JsonValue.Create(value);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, o.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* réglage non sauvegardé : l'appli reste utilisable */ }
    }
}
