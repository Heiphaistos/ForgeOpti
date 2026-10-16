using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace ForgeOpti.Core;

/// <summary>Snapshot d'un Op avant application. Exists=false : valeur/tâche absente à l'origine.</summary>
public sealed record Snap(bool Exists, string? Kind, JsonElement? Value);

/// <summary>Applique/annule des tweaks. Chaque Apply snapshote l'état d'origine (persisté) : Revert est exact.</summary>
public sealed class Engine(string stateFile)
{
    static readonly JsonSerializerOptions J = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    Dictionary<string, List<Snap>> _state = Load(stateFile);

    public bool IsApplied(string id) => _state.ContainsKey(id);

    public void Apply(Tweak t)
    {
        if (IsApplied(t.Id)) return;
        foreach (var o in t.Ops.OfType<CmdOp>())
            if (string.IsNullOrWhiteSpace(o.Revert)) throw new InvalidOperationException($"{t.Id}: CmdOp sans Revert");
        var snaps = t.Ops.Select(Capture).ToList();
        _state[t.Id] = snaps; Save(); // persisté AVANT d'agir : un crash reste annulable
        var done = 0;
        try { foreach (var o in t.Ops) { Do(o); done++; } }
        catch { for (var i = done - 1; i >= 0; i--) Undo(t.Ops[i], snaps[i]); _state.Remove(t.Id); Save(); throw; }
    }

    public void Revert(Tweak t)
    {
        if (!_state.TryGetValue(t.Id, out var snaps)) return;
        for (var i = t.Ops.Count - 1; i >= 0; i--) Undo(t.Ops[i], snaps[i]);
        _state.Remove(t.Id); Save();
    }

    // ---- registre / tâches / commandes ----
    static RegistryKey Root(string hive) =>
        RegistryKey.OpenBaseKey(hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    static Snap Capture(Op op)
    {
        switch (op)
        {
            case RegOp r:
                using (var k = Root(r.Hive).OpenSubKey(r.Key))
                {
                    var v = k?.GetValue(r.Name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    return v is null ? new(false, null, null)
                        : new(true, k!.GetValueKind(r.Name).ToString(), JsonSerializer.SerializeToElement(v));
                }
            case TaskOp t:
                var s = TaskState(t.Path);
                return new(s is not null, null, JsonSerializer.SerializeToElement(s == "Disabled"));
            default: return new(false, null, null);
        }
    }

    static void Do(Op op)
    {
        switch (op)
        {
            case RegOp r:
                using (var k = Root(r.Hive).CreateSubKey(r.Key))
                    k.SetValue(r.Name, ToObj(r.Kind, r.Value), Enum.Parse<RegistryValueKind>(r.Kind));
                break;
            case TaskOp t: if (TaskState(t.Path) is not null) Ps($"Disable-ScheduledTask {TaskArgs(t.Path)}|Out-Null"); break;
            case CmdOp c: Ps(c.Apply); break;
        }
    }

    static void Undo(Op op, Snap s)
    {
        switch (op)
        {
            case RegOp r:
                using (var k = Root(r.Hive).CreateSubKey(r.Key))
                    if (s is { Exists: true }) k.SetValue(r.Name, ToObj(s.Kind!, s.Value!.Value), Enum.Parse<RegistryValueKind>(s.Kind!));
                    else k.DeleteValue(r.Name, false);
                break;
            case TaskOp t:
                if (s is { Exists: true } && s.Value is { ValueKind: JsonValueKind.False }) Ps($"Enable-ScheduledTask {TaskArgs(t.Path)}|Out-Null");
                break;
            case CmdOp c: Ps(c.Revert); break;
        }
    }

    static object ToObj(string kind, JsonElement v) => kind switch
    {
        "DWord" => unchecked((int)(v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0)),
        "QWord" => v.GetInt64(),
        "String" or "ExpandString" => v.GetString()!,
        _ => throw new NotSupportedException($"Type de valeur non géré : {kind}"),
    };

    static string TaskArgs(string path)
    {
        var i = path.LastIndexOf('\\');
        return $"-TaskPath '{path[..(i + 1)]}' -TaskName '{path[(i + 1)..]}'";
    }

    static string? TaskState(string path)
    {
        var o = Ps($"(Get-ScheduledTask {TaskArgs(path)} -ErrorAction SilentlyContinue).State").Trim();
        return o.Length == 0 ? null : o;
    }

    public static string Ps(string script)
    {
        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {Convert.ToBase64String(Encoding.Unicode.GetBytes(script))}")
        { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        var o = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"PowerShell {p.ExitCode}: {err.Result}");
        return o;
    }

    static Dictionary<string, List<Snap>> Load(string f) =>
        File.Exists(f) ? JsonSerializer.Deserialize<Dictionary<string, List<Snap>>>(File.ReadAllText(f), J) ?? [] : [];

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);
        File.WriteAllText(stateFile, JsonSerializer.Serialize(_state, J));
    }
}
