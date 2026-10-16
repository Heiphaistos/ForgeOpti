using Microsoft.UI.Xaml;

namespace ForgeOpti.App;

public partial class App : Application
{
    Window? _w;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _w = new MainWindow(); _w.Activate();
        // Test de fumée (VM) : prouve que l'appli autonome démarre sur un Windows vierge, puis quitte.
        if (Environment.GetCommandLineArgs().Contains("--smoke"))
        {
            var t = new Microsoft.UI.Xaml.DispatcherTimer { Interval = TimeSpan.FromSeconds(45) };
            t.Tick += (_, _) => { File.WriteAllText(Path.Combine(Path.GetTempPath(), "forgeopti-smoke.ok"), "ok"); Environment.Exit(0); };
            t.Start();
        }
    }
}
