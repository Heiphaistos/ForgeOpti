using Microsoft.UI.Xaml;

namespace ForgeOpti.App;

public partial class App : Application
{
    Window? _w;
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args) { _w = new MainWindow(); _w.Activate(); }
}
