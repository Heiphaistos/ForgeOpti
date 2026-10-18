using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ForgeOpti.Core;

public static class Catalog
{
    static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };

    /// <summary>Tweaks compatibles avec le build de Windows de ce PC.</summary>
    public static List<Tweak> ForThisPc()
    {
        var build = Environment.OSVersion.Version.Build;
        return LoadAll().Where(t => t.MinBuild <= build && (t.MaxBuild == 0 || build <= t.MaxBuild)).ToList();
    }

    /// <summary>Tous les Catalog/*.json embarqués (un fichier par catégorie).</summary>
    public static List<Tweak> LoadAll()
    {
        var a = Assembly.GetExecutingAssembly();
        var all = a.GetManifestResourceNames().Where(n => n.EndsWith(".json"))
            .SelectMany(n => JsonSerializer.Deserialize<List<Tweak>>(a.GetManifestResourceStream(n)!, J)!).ToList();
        var dup = all.GroupBy(t => t.Id).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new InvalidDataException($"Id de tweak dupliqué : {dup.Key}");
        return all;
    }
}
