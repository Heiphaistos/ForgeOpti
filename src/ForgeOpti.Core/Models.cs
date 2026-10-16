using System.Text.Json.Serialization;

namespace ForgeOpti.Core;

public enum Risk { Sur, Modere, Avance }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RegOp), "reg")]
[JsonDerivedType(typeof(TaskOp), "task")]
[JsonDerivedType(typeof(CmdOp), "cmd")]
public abstract record Op;

/// <summary>Valeur de registre (couvre aussi les services : HKLM\SYSTEM\CurrentControlSet\Services\X\Start).</summary>
public sealed record RegOp(string Hive, string Key, string Name, string Kind, System.Text.Json.JsonElement Value) : Op;

/// <summary>Tâche planifiée désactivée/activée. Restaurée à son état d'origine.</summary>
public sealed record TaskOp(string Path) : Op;

/// <summary>Script PowerShell non snapshotable : Revert obligatoire (le moteur refuse sinon).</summary>
public sealed record CmdOp(string Apply, string Revert) : Op;

public sealed record Tweak(
    string Id, string Category, string Name, string Description,
    Risk Risk, bool Reboot, bool Gaming, bool OneClick, List<Op> Ops, int MinBuild = 0, int MaxBuild = 0);

