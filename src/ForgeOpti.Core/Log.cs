namespace ForgeOpti.Core;

/// <summary>Journal unique ProgramData\ForgeOpti\forgeopti.log, format [ISO8601] [NIVEAU] message, rotation au-delà de 1 Mo.</summary>
public static class Log
{
    static readonly object L = new();
    public static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ForgeOpti");
    public static string FilePath => Path.Combine(Dir, "forgeopti.log");

    public static void Info(string m) => Write("INFO", m);
    public static void Warn(string m) => Write("WARN", m);
    public static void Error(string m) => Write("ERROR", m);

    static void Write(string level, string m)
    {
        try
        {
            lock (L)
            {
                Directory.CreateDirectory(Dir);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > 1 << 20) File.Move(FilePath, FilePath + ".1", true);
                File.AppendAllText(FilePath, $"[{DateTime.Now:s}] [{level}] {m}{Environment.NewLine}");
            }
        }
        catch { /* le journal ne doit jamais faire planter l'appli */ }
    }

    /// <summary>Dernières lignes du journal (page Santé).</summary>
    public static List<string> Tail(int n)
    {
        try { return File.ReadAllLines(FilePath).TakeLast(n).ToList(); }
        catch { return []; }
    }
}
