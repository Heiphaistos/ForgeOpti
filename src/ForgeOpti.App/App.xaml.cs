using ForgeOpti.Core;
using Microsoft.UI.Xaml;

namespace ForgeOpti.App;

public partial class App : Application
{
    Window? _w;
    public App()
    {
        InitializeComponent();
        // Aucune exception isolée ne doit fermer l'appli : on journalise et on continue (l'état des réglages est enregistré avant chaque action).
        // Nomme la ressource XAML introuvable quand un contrôle échoue à s'afficher (la cause de l'exception sinon opaque).
        DebugSettings.XamlResourceReferenceFailed += (_, e) => Log.Warn("Ressource XAML : " + e.Message);
        DebugSettings.BindingFailed += (_, e) => Log.Warn("Liaison XAML : " + e.Message);
        UnhandledException += (_, e) => { Log.Error($"Exception non gérée : {e.Message} | {e.Exception}"); e.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Exception fatale : " + e.ExceptionObject);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Error("Tâche en erreur : " + e.Exception); e.SetObserved(); };
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var argv = Environment.GetCommandLineArgs();
        if (argv.Contains("--silent-update")) { _ = SilentUpdate(); return; }
        if (argv.Contains("--repair")) // tâche planifiée d'ouverture de session : sans interface
        {
            try { Maintenance.Repair(new Engine(Maintenance.StatePath), Catalog.ForThisPc()); }
            catch (Exception e) { Log.Error("Vérification planifiée : " + e); }
            Environment.Exit(0);
        }
        _w = new MainWindow(); _w.Activate();
        // Test de fumée (VM) : prouve que l'appli autonome démarre sur un Windows vierge, puis quitte.
        if (argv.Contains("--smoke"))
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            t.Tick += (_, _) => { File.WriteAllText(Path.Combine(Path.GetTempPath(), "forgeopti-smoke.ok"), "ok"); Environment.Exit(0); };
            t.Start();
        }
    }

    /// <summary>Mise à jour sans interface (tâche planifiée / test) : journal dans ProgramData\ForgeOpti\update.log.</summary>
    static async Task SilentUpdate()
    {
        var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ForgeOpti", "update.log");
        Directory.CreateDirectory(Path.GetDirectoryName(log)!);
        void L(string m) => File.AppendAllText(log, $"[{DateTime.Now:s}] {m}{Environment.NewLine}");
        try
        {
            L($"version {Updater.Current}");
            var u = await Updater.CheckAsync();
            if (u is null) { L("à jour"); Environment.Exit(0); }
            L($"mise à jour {u!.Version} trouvée, installation");
            await Updater.InstallAsync(u);
        }
        catch (Exception e) { L("ECHEC : " + e.Message); Environment.Exit(1); }
    }
}
