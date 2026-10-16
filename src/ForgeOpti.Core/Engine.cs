using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace ForgeOpti.Core;

/// <summary>Snapshot d'un Op avant application. Exists=false : valeur/tâche absente à l'origine.</summary>
public sealed record Snap(bool Exists, string? Kind, JsonElement? Value);

/// <summary>État persisté : tweaks appliqués, valeur d'origine de chaque emplacement (1re prise gagne), et qui la "réclame".</summary>
public sealed class State
{
    public Dictionary<string, bool> Applied { get; set; } = [];
    public Dictionary<string, Snap> Origins { get; set; } = [];
    public Dictionary<string, List<string>> Claims { get; set; } = [];
}

/// <summary>
/// Applique/annule des tweaks. Plusieurs tweaks peuvent toucher le même emplacement (clé de registre, tâche) :
/// l'original n'est capturé qu'une fois et n'est restauré que lorsque le dernier tweak qui le touche est annulé.
/// </summary>
public sealed class Engine(string stateFile)
{
    static readonly JsonSerializerOptions J = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    readonly State _s = Load(stateFile);

    public bool IsApplied(string id) => _s.Applied.ContainsKey(id);

    static string? Loc(Op op) => op switch
    {
        RegOp r => $"reg|{r.Hive}|{r.Key}|{r.Name}".ToLowerInvariant(),
        TaskOp t => $"task|{t.Path}".ToLowerInvariant(),
        _ => null,
    };

    public void Apply(Tweak t)
    {
        if (IsApplied(t.Id)) return;
        foreach (var o in t.Ops.OfType<CmdOp>())
            if (string.IsNullOrWhiteSpace(o.Revert)) throw new InvalidOperationException($"{t.Id}: CmdOp sans Revert");
        foreach (var o in t.Ops)
            if (Loc(o) is { } l)
            {
                if (!_s.Origins.ContainsKey(l)) _s.Origins[l] = Capture(o);
                if (!_s.Claims.TryGetValue(l, out var c)) _s.Claims[l] = c = [];
                if (!c.Contains(t.Id)) c.Add(t.Id);
            }
        _s.Applied[t.Id] = true; Save(); // persisté AVANT d'agir : un crash reste annulable
        var done = 0;
        try { foreach (var o in t.Ops) { Do(o); done++; } }
        catch
        {
            for (var i = done - 1; i >= 0; i--) { try { Undo(t.Ops[i], t.Id); } catch { /* meilleur effort */ } }
            foreach (var o in t.Ops.Skip(done)) Release(o, t.Id);
            _s.Applied.Remove(t.Id); Save(); throw;
        }
    }

    /// <summary>Annule : registre/tâches d'abord (sûr), puis commandes. Si une commande échoue, le tweak reste "appliqué" pour pouvoir réessayer.</summary>
    public void Revert(Tweak t)
    {
        if (!IsApplied(t.Id)) return;
        foreach (var o in t.Ops.Where(o => o is not CmdOp).Reverse()) Undo(o, t.Id);
        List<string> errs = [];
        foreach (var o in t.Ops.Where(o => o is CmdOp).Reverse())
            try { Undo(o, t.Id); } catch (Exception e) { errs.Add(e.Message); }
        Save();
        if (errs.Count > 0) throw new InvalidOperationException(string.Join(" | ", errs));
        _s.Applied.Remove(t.Id); Save();
    }

    void Release(Op o, string id) { if (Loc(o) is { } l && _s.Claims.TryGetValue(l, out var c)) c.Remove(id); }

    // ---- registre / tâches / commandes ----
    static RegistryKey Root(string hive) =>
        RegistryKey.OpenBaseKey(hive == "HKLM" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, RegistryView.Registry64);

    /// <summary>Etat observable d'un Op (comparaison avant/après). null si non sondable (CmdOp).</summary>
    public static string? Probe(Op op) => op switch
    {
        RegOp r => Capture(r) is { Exists: true } c ? $"{c.Kind}:{c.Value}" : "absent",
        TaskOp t => TaskState(t.Path) ?? "absent",
        _ => null,
    };

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

    void Undo(Op op, string id)
    {
        if (op is CmdOp c) { Ps(c.Revert); return; }
        var l = Loc(op)!;
        if (!_s.Claims.TryGetValue(l, out var claims) || !claims.Remove(id)) return; // déjà libéré
        if (claims.Count > 0) return; // un autre tweak actif touche encore cet emplacement : on ne restaure pas
        _s.Claims.Remove(l);
        var snap = _s.Origins[l]; _s.Origins.Remove(l);
        switch (op)
        {
            case RegOp r:
                using (var k = Root(r.Hive).CreateSubKey(r.Key))
                    if (snap is { Exists: true }) k.SetValue(r.Name, ToObj(snap.Kind!, snap.Value!.Value), Enum.Parse<RegistryValueKind>(snap.Kind!));
                    else k.DeleteValue(r.Name, false);
                break;
            case TaskOp t:
                if (snap is { Exists: true } && snap.Value is { ValueKind: JsonValueKind.False }) Ps($"Enable-ScheduledTask {TaskArgs(t.Path)}|Out-Null");
                break;
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

    const string Pre = """
        $ProgressPreference='SilentlyContinue'
        if (-not (Get-Command winget -EA 0)) {
          $w = Get-ChildItem "$env:ProgramFiles\WindowsApps\Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe\winget.exe" -EA 0 | Sort-Object FullName -Descending | Select-Object -First 1
          if ($w) { Set-Alias winget $w.FullName -Scope Global }
        }

        """;

    public static string Ps(string script)
    {
        var psi = new ProcessStartInfo("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -OutputFormat Text -EncodedCommand {Convert.ToBase64String(Encoding.Unicode.GetBytes(Pre + script))}")
        { RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        var o = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new InvalidOperationException($"PowerShell {p.ExitCode}: {err.Result}");
        return o;
    }

    static State Load(string f) =>
        File.Exists(f) ? JsonSerializer.Deserialize<State>(File.ReadAllText(f), J) ?? new() : new();

    void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);
        File.WriteAllText(stateFile, JsonSerializer.Serialize(_s, J));
    }
}
