using ForgeOpti.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ForgeOpti.App;

public sealed partial class MainWindow : Window
{
    const string Gaming = "★ Mode gaming";
    const string CleanCat = "🧹 Nettoyage";
    static readonly int Build = Environment.OSVersion.Version.Build;
    readonly Engine _engine = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ForgeOpti", "state.json"));
    readonly List<Tweak> _all = Catalog.LoadAll().Where(t => t.MinBuild <= Build && (t.MaxBuild == 0 || Build <= t.MaxBuild)).ToList();
    bool _restoreDone, _busy, _refreshing;

    public MainWindow()
    {
        InitializeComponent();
        Title = "ForgeOpti";
        try
        {
            // Taille confortable : 70 % x 80 % de la zone de travail (min 1000x700), centrée.
            var wa = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
            int w = Math.Min(Math.Max(1000, wa.Width * 7 / 10), wa.Width), h = Math.Min(Math.Max(700, wa.Height * 8 / 10), wa.Height);
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h));
        }
        catch { /* taille par défaut */ }
        try { AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "forgeopti.ico")); } catch { /* icône facultative */ }
        foreach (var c in new[] { Gaming, CleanCat }.Concat(_all.Select(t => t.Category).Distinct().Order())) Cats.Items.Add(c);
        Cats.SelectedIndex = 0;
        ChkAuto.IsChecked = Updater.Enabled;
        if (Updater.Enabled) _ = CheckUpdate(false);
    }

    void Auto_Click(object s, RoutedEventArgs e) => Updater.Enabled = ChkAuto.IsChecked == true;
    async void Update_Click(object s, RoutedEventArgs e) => await CheckUpdate(true);

    async Task CheckUpdate(bool manual)
    {
        try
        {
            Status.Text = "Recherche de mise à jour…";
            var u = await Updater.CheckAsync();
            if (u is null) { Status.Text = manual ? "ForgeOpti est à jour." : "Prêt."; return; }
            var d = new ContentDialog { XamlRoot = Root.XamlRoot, Title = $"Mise à jour {u.Version} disponible", Content = "Télécharger, vérifier (SHA-256) et installer maintenant ? L'application redémarrera.", PrimaryButtonText = "Installer", CloseButtonText = "Plus tard" };
            if (await d.ShowAsync() == ContentDialogResult.Primary) { Status.Text = "Téléchargement…"; await Updater.InstallAsync(u); }
            else Status.Text = "Prêt.";
        }
        catch (Exception ex) { Status.Text = manual ? "Mise à jour impossible : " + ex.Message : "Prêt."; }
    }

    void Cats_SelectionChanged(object s, SelectionChangedEventArgs e) => Show();

    void Show()
    {
        var cat = (string?)Cats.SelectedItem; if (cat is null) return;
        if (cat == CleanCat) { ShowCleaner(); return; }
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

    void ShowCleaner()
    {
        List.Children.Clear();
        List.Children.Add(new TextBlock { Text = "Libère de l'espace disque. Les éléments supprimés sont des caches et fichiers temporaires que Windows recrée : ce nettoyage n'est pas annulable.", Opacity = 0.7, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12, 0, 12, 8) });
        var boxes = new List<(CheckBox Box, CleanItem Item, TextBlock Size)>();
        foreach (var it in Cleaner.Items)
        {
            var size = new TextBlock { Text = "…", Opacity = 0.65, VerticalAlignment = VerticalAlignment.Center };
            var cb = new CheckBox { Content = $"{it.Name}  —  {it.Description}", IsChecked = it.Id != "composants" };
            var g = new Grid { Padding = new Thickness(12, 4, 12, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(cb); Grid.SetColumn(size, 1); g.Children.Add(size);
            List.Children.Add(g); boxes.Add((cb, it, size));
            _ = Task.Run(() => Cleaner.Measure(it)).ContinueWith(t => DispatcherQueue.TryEnqueue(() => size.Text = it.Special is not null && it.Dirs().All(d => false) ? "" : Human(t.Result)));
        }
        var go = new Button { Content = "🧹 Nettoyer la sélection", Style = (Style)Application.Current.Resources["AccentButtonStyle"], Margin = new Thickness(12, 12, 12, 0) };
        go.Click += async (_, _) => { if (!_busy) await Run(async () =>
        {
            long freed = 0;
            foreach (var (box, item, size) in boxes.Where(b => b.Box.IsChecked == true))
            {
                Status.Text = $"Nettoyage : {item.Name}…";
                freed += await Task.Run(() => Cleaner.Clean(item));
                size.Text = Human(await Task.Run(() => Cleaner.Measure(item)));
            }
            Status.Text = $"Nettoyage terminé : {Human(freed)} libérés.";
        }, () => { }); };
        List.Children.Add(go);
    }

    static string Human(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} Go" : b >= 1L << 20 ? $"{b / (double)(1L << 20):0} Mo" : b > 0 ? $"{b / 1024} Ko" : "0";

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
        var todo = _all.Where(t => _engine.IsApplied(t.Id)).ToList();
        int n = 0, fail = 0;
        foreach (var t in todo)
        {
            Status.Text = $"Annulation ({++n}/{todo.Count}) : {t.Name}…";
            try { await Task.Run(() => _engine.Revert(t)); } catch { fail++; }
        }
        Status.Text = fail == 0 ? "Tout a été annulé." : $"Annulation terminée : {todo.Count - fail} annulés, {fail} à réessayer (réinstallation impossible pour le moment).";
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
