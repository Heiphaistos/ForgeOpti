using ForgeOpti.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ForgeOpti.App;

public sealed partial class MainWindow : Window
{
    static readonly int Build = Environment.OSVersion.Version.Build;
    readonly Engine _engine = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ForgeOpti", "state.json"));
    readonly List<Tweak> _all = Catalog.LoadAll().Where(t => t.MinBuild <= Build && (t.MaxBuild == 0 || Build <= t.MaxBuild)).ToList();
    Theme _theme = Theme.All[0];
    string _navMode = "side", _view = "cards", _page = Nav.Home;
    bool _restoreDone, _busy, _refreshing;
    readonly List<Button> _actionButtons = [];
    List<Button> _headerButtons = [];

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

        _navMode = Settings.Get("nav", "side");
        _view = Settings.Get("view", "cards");
        var start = Settings.Get("page", Nav.Home); // dernière page ouverte
        _page = start is Nav.Home or Nav.Clean or Nav.GamingAll || _all.Any(t => t.Category == start) ? start : Nav.Home;
        ApplyAppearance(Settings.Get("theme", "forge"));
        if (Updater.Enabled) _ = CheckUpdate(false);
    }

    // ---------- mises à jour ----------
    async Task CheckUpdate(bool manual)
    {
        try
        {
            Status.Text = "Recherche de mise à jour…";
            var u = await Updater.CheckAsync();
            if (u is null) { Status.Text = manual ? "ForgeOpti est à jour." : "Prêt."; return; }
            var d = Dialog($"Mise à jour {u.Version} disponible", "Télécharger, vérifier (SHA-256) et installer maintenant ? L'application redémarrera.", "Installer", "Plus tard");
            if (await d.ShowAsync() == ContentDialogResult.Primary) { Status.Text = "Téléchargement…"; await Updater.InstallAsync(u); }
            else Status.Text = "Prêt.";
        }
        catch (Exception ex) { Status.Text = manual ? "Mise à jour impossible : " + ex.Message : "Prêt."; }
    }

    ContentDialog Dialog(string title, string body, string primary, string close) => new()
    {
        XamlRoot = Root.XamlRoot, Title = title, Content = body, PrimaryButtonText = primary, CloseButtonText = close,
        DefaultButton = ContentDialogButton.Primary, RequestedTheme = Root.RequestedTheme,
    };

    // ---------- exécution ----------
    async Task Run(Func<Task> work, Action refresh)
    {
        _busy = true; Busy.IsActive = true;
        foreach (var b in _actionButtons) b.IsEnabled = false;
        try { await work(); }
        catch (Exception ex) { Status.Text = "Erreur : " + ex.Message; }
        finally
        {
            _busy = false; Busy.IsActive = false;
            foreach (var b in _actionButtons) b.IsEnabled = true;
            refresh();
        }
    }

    async Task EnsureRestorePoint()
    {
        if (_restoreDone) return;
        Status.Text = "Création du point de restauration…";
        await Task.Run(() => RestorePoint.Create("ForgeOpti – avant optimisation"));
        _restoreDone = true;
    }

    /// <summary>Applique une liste de réglages après confirmation et point de restauration. Un échec ne bloque pas les suivants.</summary>
    async Task ApplyMany(string title, List<Tweak> todo)
    {
        if (_busy) return;
        if (todo.Count == 0) { Status.Text = "Rien à appliquer : tout est déjà actif."; return; }
        var d = Dialog(title, $"{todo.Count} réglage(s) vont être appliqués après la création d'un point de restauration. Tu pourras tout annuler.", "Appliquer", "Annuler");
        if (await d.ShowAsync() != ContentDialogResult.Primary) return;
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
        }, () => Go(_page));
    }

    async Task RevertMany(List<Tweak> list, string doneText)
    {
        if (_busy) return;
        var todo = list.Where(t => _engine.IsApplied(t.Id)).ToList();
        if (todo.Count == 0) { Status.Text = "Rien à annuler."; return; }
        await Run(async () =>
        {
            int n = 0, fail = 0;
            foreach (var t in todo)
            {
                Status.Text = $"Annulation ({++n}/{todo.Count}) : {t.Name}…";
                try { await Task.Run(() => _engine.Revert(t)); } catch { fail++; }
            }
            Status.Text = fail == 0 ? doneText : $"Annulation terminée : {todo.Count - fail} annulés, {fail} à réessayer (réinstallation impossible pour le moment).";
        }, () => Go(_page));
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
        }, () => { _refreshing = true; sw.IsOn = _engine.IsApplied(t.Id); _refreshing = false; BuildNav(); });
    }

    // ---------- actions d'en-tête ----------
    List<Tweak> OneClickSet => _all.Where(t => t.OneClick && !_engine.IsApplied(t.Id)).ToList();
    async void OneClick_Click(object s, RoutedEventArgs e) => await ApplyMany("Optimiser en 1 clic", OneClickSet);
    async void UndoAll_Click(object s, RoutedEventArgs e) => await RevertMany(_all, "Tout a été annulé.");
    async void Update_Click(object s, RoutedEventArgs e) => await CheckUpdate(true);
}
