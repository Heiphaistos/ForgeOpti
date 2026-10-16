using ForgeOpti.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ForgeOpti.App;

public sealed partial class MainWindow : Window
{
    const string Gaming = "★ Mode gaming";
    static readonly int Build = Environment.OSVersion.Version.Build;
    readonly Engine _engine = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ForgeOpti", "state.json"));
    readonly List<Tweak> _all = Catalog.LoadAll().Where(t => t.MinBuild <= Build).ToList();
    bool _restoreDone, _busy, _refreshing;

    public MainWindow()
    {
        InitializeComponent();
        Title = "ForgeOpti";
        foreach (var c in new[] { Gaming }.Concat(_all.Select(t => t.Category).Distinct().Order())) Cats.Items.Add(c);
        Cats.SelectedIndex = 0;
    }

    void Cats_SelectionChanged(object s, SelectionChangedEventArgs e) => Show();

    void Show()
    {
        var cat = (string?)Cats.SelectedItem; if (cat is null) return;
        _refreshing = true; List.Children.Clear();
        foreach (var t in _all.Where(t => cat == Gaming ? t.Gaming : t.Category == cat))
        {
            var sw = new ToggleSwitch { IsOn = _engine.IsApplied(t.Id), Tag = t, OnContent = "", OffContent = "" };
            sw.Toggled += Toggled;
            var txt = new StackPanel { Spacing = 2 };
            txt.Children.Add(new TextBlock { Text = t.Name + (t.Reboot ? "  (redémarrage requis)" : ""), TextWrapping = TextWrapping.Wrap });
            txt.Children.Add(new TextBlock { Text = $"{t.Description}  —  Risque : {t.Risk}", Opacity = 0.65, FontSize = 12, TextWrapping = TextWrapping.Wrap });
            var g = new Grid { Padding = new Thickness(12, 8, 12, 8), ColumnSpacing = 12 };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(txt); Grid.SetColumn(sw, 1); g.Children.Add(sw);
            List.Children.Add(g);
        }
        _refreshing = false;
    }

    async void Toggled(object s, RoutedEventArgs e)
    {
        if (_refreshing || _busy) return;
        var sw = (ToggleSwitch)s; var t = (Tweak)sw.Tag;
        await Run(async () =>
        {
            if (sw.IsOn) { await EnsureRestorePoint(); await Task.Run(() => _engine.Apply(t)); }
            else await Task.Run(() => _engine.Revert(t));
            Status.Text = $"{(sw.IsOn ? "Appliqué" : "Annulé")} : {t.Name}";
        }, () => { _refreshing = true; sw.IsOn = _engine.IsApplied(t.Id); _refreshing = false; });
    }

    async void OneClick_Click(object s, RoutedEventArgs e)
    {
        var todo = _all.Where(t => t.OneClick && !_engine.IsApplied(t.Id)).ToList();
        var d = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, Title = "Optimiser en 1 clic",
            Content = $"{todo.Count} réglages sûrs vont être appliqués après la création d'un point de restauration. Tu pourras tout annuler.",
            PrimaryButtonText = "Optimiser", CloseButtonText = "Annuler", DefaultButton = ContentDialogButton.Primary,
        };
        if (todo.Count == 0 || await d.ShowAsync() != ContentDialogResult.Primary) return;
        await Run(async () =>
        {
            await EnsureRestorePoint();
            int n = 0, fail = 0;
            foreach (var t in todo)
            {
                Status.Text = $"({++n}/{todo.Count}) {t.Name}…";
                try { await Task.Run(() => _engine.Apply(t)); } catch { fail++; }
            }
            Status.Text = $"Terminé : {todo.Count - fail} appliqués, {fail} échecs (annulés automatiquement).";
        }, Show);
    }

    async void UndoAll_Click(object s, RoutedEventArgs e) => await Run(async () =>
    {
        foreach (var t in _all.Where(t => _engine.IsApplied(t.Id))) await Task.Run(() => _engine.Revert(t));
        Status.Text = "Tout a été annulé.";
    }, Show);

    async Task EnsureRestorePoint()
    {
        if (_restoreDone) return;
        Status.Text = "Création du point de restauration…";
        await Task.Run(() => RestorePoint.Create("ForgeOpti – avant optimisation"));
        _restoreDone = true;
    }

    async Task Run(Func<Task> work, Action refresh)
    {
        _busy = true; BtnOneClick.IsEnabled = BtnUndoAll.IsEnabled = false;
        try { await work(); }
        catch (Exception ex) { Status.Text = "Erreur : " + ex.Message; }
        finally { _busy = false; BtnOneClick.IsEnabled = BtnUndoAll.IsEnabled = true; refresh(); }
    }
}
