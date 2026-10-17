using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;

// Aucun assistant, aucune installation : extrait l'appli une seule fois par version, puis la lance directement.
var asm = Assembly.GetExecutingAssembly();
var version = asm.GetName().Version?.ToString(3) ?? "0";
var target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ForgeOpti-Portable", version);
var exe = Path.Combine(target, "ForgeOpti.exe");
var marker = Path.Combine(target, ".extrait");

try
{
    using var mutex = new Mutex(false, @"Global\ForgeOptiPortableExtract");
    mutex.WaitOne();
    try
    {
        if (!File.Exists(marker))
        {
            // 1re exécution : petite fenêtre d'attente (l'extraction peut prendre quelques secondes), messages pompés pendant le travail.
            var splash = Native.Splash("ForgeOpti — première exécution : préparation des fichiers, quelques secondes…");
            var work = Task.Run(() =>
            {
                using var res = asm.GetManifestResourceStream("payload.br") ?? throw new InvalidDataException("charge utile absente");
                using var br = new BrotliStream(res, CompressionMode.Decompress);
                using var ms = new MemoryStream();
                br.CopyTo(ms); ms.Position = 0;
                if (Directory.Exists(target)) Directory.Delete(target, true);
                new ZipArchive(ms).ExtractToDirectory(target, true);
                File.WriteAllText(marker, version);
            });
            Native.Pump(work);
            Native.DestroyWindow(splash);
            work.GetAwaiter().GetResult();
        }
    }
    finally { mutex.ReleaseMutex(); }

    Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = target });
}
catch (Exception e)
{
    Native.MessageBoxW(IntPtr.Zero, "ForgeOpti n'a pas pu démarrer :\n" + e.Message, "ForgeOpti", 0x10);
}

static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    struct Msg { public IntPtr hwnd; public uint message; public UIntPtr wParam; public IntPtr lParam; public uint time; public int x, y; }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int MessageBoxW(IntPtr h, string text, string caption, uint type);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowExW(uint ex, string cls, string text, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr p);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] static extern bool PeekMessageW(out Msg m, IntPtr h, uint min, uint max, uint remove);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref Msg m);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref Msg m);

    /// <summary>Fenêtre « STATIC » native (sans classe à enregistrer), centrée, toujours au premier plan.</summary>
    public static IntPtr Splash(string text)
    {
        const int w = 520, h = 70;
        // WS_POPUP | WS_VISIBLE | WS_BORDER | SS_CENTER | SS_CENTERIMAGE ; WS_EX_TOPMOST | WS_EX_TOOLWINDOW
        return CreateWindowExW(0x8 | 0x80, "STATIC", text, 0x80000000 | 0x10000000 | 0x800000 | 0x1 | 0x200,
            (GetSystemMetrics(0) - w) / 2, (GetSystemMetrics(1) - h) / 2, w, h, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Pump(Task work)
    {
        while (!work.IsCompleted)
        {
            while (PeekMessageW(out var m, IntPtr.Zero, 0, 0, 1)) { TranslateMessage(ref m); DispatchMessageW(ref m); }
            Thread.Sleep(30);
        }
    }
}
