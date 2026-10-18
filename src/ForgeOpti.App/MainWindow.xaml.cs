using ForgeOpti.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ForgeOpti.App;

public sealed partial class MainWindow : Window
{
    static readonly int Build = Environment.OSVersion.Version.Build;
    readonly Engine _engine = new(Maintenance.StatePath);
    readonly List<Tweak> _all = Catalog.ForThisPc();
    /// <summary>Sélection en attente : id -> état voulu (true = activer, false = désactiver). Rien n'est appliqué avant validation.</summary>
    readonly Dictionary<string, bool> _want = [];
    readonly List<Microsoft.UI.Xaml.Controls.Primitives.ToggleButton> _switches = []; // interrupteurs de la page affichée
    TextBlock? _subtitle;
    Theme _theme = Theme.All[0];
    string _navMode = "side", _view = "cards", _page = Nav.Home;
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

        _navMode = Settings.Get("nav", "side");
        _view = Settings.Get("view", "cards");
        var start = Settings.Get("page", Nav.Home); // dernière page ouverte
        _page = start is Nav.Home or Nav.Clean or Nav.Health or Nav.GamingAll || _all.Any(t => t.Category == start) ? start : Nav.Home;
        ApplyAppearance(Settings.Get("theme", "forge"));
        if (Updater.Enabled) _ = CheckUpdate(false);
        MainScroll.IsTabStop = true; MainScroll.FocusVisualPrimaryThickness = MainScroll.FocusVisualSecondaryThickness = new Thickness(0); // cible de focus invisible
        _ = StartupCheck();
    }

    /// <summary>Au lancement : remet en place ce qu'une mise à jour Windows a défait, et garde la tâche de vérification à jour.</summary>
    async Task StartupCheck()
    {
        try
        {
            if (!Maintenance.AutoRepair) return;
            var res = await Task.Run(() =>
            {
                var r = Maintenance.Repair(_engine, _all);
                if (_all.Any(t => _engine.IsApplied(t.Id))) Maintenance.SyncTask(true);
                return r;
            });
            if (res.Count == 0 || _busy) return;
            int ok = res.Count(r => r.Error is null);
            Status.Text = $"{ok} réglage(s) remis en place après une mise à jour Windows" + (ok < res.Count ? $", {res.Count - ok} en échec (voir Santé)." : ".");
        }
        catch (Exception e) { Log.Error("Vérification au démarrage : " + e); }
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

    /// <summary>Boîte de dialogue aux couleurs du thème (le bouton principal suit l'accent choisi, pas celui de Windows).</summary>
    ContentDialog Dialog(string title, object body, string primary, string close)
    {
        var t = _theme;
        var d = new ContentDialog
        {
            XamlRoot = Root.XamlRoot, Title = title, Content = body, PrimaryButtonText = primary, CloseButtonText = close,
            DefaultButton = ContentDialogButton.Primary, RequestedTheme = Root.RequestedTheme, Background = t.SurfaceB, Foreground = t.TextB,
        };
        foreach (var (k, v) in new (string, Microsoft.UI.Xaml.Media.Brush)[]
        {
            ("AccentButtonBackground", t.AccentB), ("AccentButtonBackgroundPointerOver", t.B(t.Accent, 0xDD)), ("AccentButtonBackgroundPressed", t.B(t.Accent, 0xB0)),
            ("AccentButtonForeground", t.OnAccentB), ("AccentButtonForegroundPointerOver", t.OnAccentB), ("AccentButtonForegroundPressed", t.OnAccentB),
            ("ContentDialogBackground", t.SurfaceB), ("ContentDialogForeground", t.TextB), ("ContentDialogBorderBrush", t.BorderB),
        }) d.Resources[k] = v;
        return d;
    }

    // ---------- exécution ----------
    /// <summary>Exécute un travail long en verrouillant l'interface (aucun changement de page ni de sélection pendant l'application).</summary>
    async Task Run(Func<Task> work, Action refresh)
    {
        SetBusy(true);
        try { await work(); }
        catch (Exception ex) { Log.Error(ex.ToString()); Status.Text = "Erreur : " + ex.Message; }
        finally
        {
            SetBusy(false);
            try { refresh(); } catch (Exception ex) { Log.Error("Rafraîchissement : " + ex); }
        }
    }

    void SetBusy(bool b)
    {
        _busy = b; Busy.IsIndeterminate = b; Busy.Visibility = b ? Visibility.Visible : Visibility.Collapsed;
        BusyShield.Visibility = b ? Visibility.Visible : Visibility.Collapsed;
    }

    async Task EnsureRestorePoint()
    {
        if (_restoreDone) return;
        Status.Text = "Création du point de restauration…";
        await Task.Run(() => RestorePoint.Create("ForgeOpti – avant optimisation"));
        _restoreDone = true;
    }

    // ---------- sélection en attente ----------
    bool Want(Tweak t) => _want.TryGetValue(t.Id, out var v) ? v : _engine.IsApplied(t.Id);

    void SetWant(Tweak t, bool on)
    {
        if (on == _engine.IsApplied(t.Id)) _want.Remove(t.Id); else _want[t.Id] = on;
        UpdatePending();
    }

    void UpdatePending()
    {
        int on = _want.Count(kv => kv.Value), off = _want.Count - on;
        // Masquer la barre alors qu'un de ses boutons a le focus déclenche une exception XAML (« Invalid attribute value Unknown for property Stroke »,
        // le cadre de focus) et fige l'interface : on déplace d'abord le focus vers une zone qui ne disparaît pas.
        if (_want.Count == 0 && PendingBar.Visibility == Visibility.Visible) MainScroll.Focus(FocusState.Programmatic);
        PendingBar.Visibility = _want.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        PendingText.Text = $"{_want.Count} modification(s) en attente : {on} à activer, {off} à désactiver. Rien n'est appliqué tant que tu n'as pas validé.";
    }

    /// <summary>
    /// Après une application : met à jour les interrupteurs et les compteurs EN PLACE. Reconstruire toute la page à cet instant
    /// déclenchait une exception XAML (« Invalid attribute value Unknown for property Stroke ») constatée en VM.
    /// </summary>
    void RefreshInPlace()
    {
        if (_switches.Count == 0) { Go(_page); return; }
        _refreshing = true;
        try { foreach (var sw in _switches) sw.IsChecked = Want((Tweak)sw.Tag); }
        finally { _refreshing = false; }
        if (_subtitle is not null && _page is not (Nav.Search or Nav.Home))
        {
            var (a, n) = Counts(_page);
            _subtitle.Text = $"{n} réglages · {a} actifs";
        }
        BuildNav();
    }

    void ClearWant() { _want.Clear(); UpdatePending(); }

    /// <summary>Présélectionne des réglages (sans rien appliquer) puis réaffiche la page pour que les interrupteurs suivent.</summary>
    void Preselect(string label, IEnumerable<Tweak> list, bool on = true)
    {
        if (_busy) return;
        int n = 0;
        foreach (var t in list) if (_engine.IsApplied(t.Id) != on) { _want[t.Id] = on; n++; } else _want.Remove(t.Id);
        UpdatePending(); BuildPage();
        Status.Text = n == 0 ? $"{label} : rien à changer." : $"{label} : {n} réglage(s) présélectionné(s). Ajuste dans les catégories si tu veux, puis « Voir et appliquer ».";
    }

    object Preview(string intro, List<Tweak> on, List<Tweak> off)
    {
        var t = _theme; var s = new StackPanel { Spacing = 4 };
        s.Children.Add(Widgets.Text(intro, 13, t));
        void Part(string head, List<Tweak> l, string color)
        {
            if (l.Count == 0) return;
            s.Children.Add(new TextBlock { Text = $"{head} ({l.Count})", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = t.B(color), Margin = new Thickness(0, 8, 0, 0) });
            foreach (var x in l.OrderBy(x => x.Name)) s.Children.Add(Widgets.Text("•  " + x.Name + (x.Reboot ? "  (redémarrage)" : ""), 12.5, t, sub: true));
        }
        Part("À activer", on, t.Accent); Part("À désactiver", off, t.Sub);
        return new ScrollViewer { Content = s, MaxHeight = 380, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>
    /// Applique la sélection en attente après aperçu et confirmation, puis point de restauration. Un échec ne bloque pas les suivants ;
    /// chaque activation est relue dans le registre, et un compte rendu précis est affiché.
    /// </summary>
    async Task ApplyPending(string title, bool clearOnCancel = false)
    {
        if (_busy) return;
        var byId = _all.ToDictionary(t => t.Id);
        var on = _want.Where(kv => kv.Value && byId.ContainsKey(kv.Key) && !_engine.IsApplied(kv.Key)).Select(kv => byId[kv.Key]).ToList();
        var off = _want.Where(kv => !kv.Value && byId.ContainsKey(kv.Key) && _engine.IsApplied(kv.Key)).Select(kv => byId[kv.Key]).ToList();
        if (on.Count + off.Count == 0) { ClearWant(); Status.Text = "Rien à appliquer."; return; }
        var intro = on.Count > 0 ? "Un point de restauration Windows sera créé avant les changements. Tout reste annulable." : "Ces réglages vont être annulés (valeurs d'origine remises).";
        if (await Dialog(title, Preview(intro, on, off), "Appliquer", "Annuler").ShowAsync() != ContentDialogResult.Primary)
        {
            if (clearOnCancel) { ClearWant(); BuildPage(); }
            return;
        }

        List<string> problems = [];
        int done = 0, total = on.Count + off.Count;
        await Run(async () =>
        {
            if (on.Count > 0) await EnsureRestorePoint();
            foreach (var t in off)
            {
                Status.Text = $"({++done}/{total}) Annulation : {t.Name}…";
                try { await Task.Run(() => _engine.Revert(t)); Log.Info("Annulé : " + t.Id); }
                catch (Exception e) { problems.Add($"{t.Name} (annulation) : {Short(e.Message)}"); Log.Error($"Annulation {t.Id} : {e.Message}"); }
            }
            foreach (var t in on)
            {
                Status.Text = $"({++done}/{total}) Application : {t.Name}…";
                try
                {
                    await Task.Run(() => _engine.Apply(t));
                    var bad = await Task.Run(() => _engine.Drift(t));
                    if (bad.Count > 0) { problems.Add($"{t.Name} : appliqué mais {bad.Count} valeur(s) non confirmée(s) dans le registre"); Log.Warn($"Non confirmé {t.Id}"); }
                    else Log.Info("Appliqué : " + t.Id);
                }
                catch (Exception e) { problems.Add($"{t.Name} : {Short(e.Message)} (annulé automatiquement)"); Log.Error($"Application {t.Id} : {e.Message}"); }
            }
            if (Maintenance.AutoRepair && _all.Any(t => _engine.IsApplied(t.Id))) await Task.Run(() => Maintenance.SyncTask(true));
            Status.Text = problems.Count == 0 ? $"Terminé : {total} changement(s) appliqué(s) et vérifié(s)." : $"Terminé : {total - problems.Count} réussi(s), {problems.Count} problème(s).";
        }, () => { ClearWant(); RefreshInPlace(); });

        if (problems.Count > 0)
        {
            var t = _theme; var s = new StackPanel { Spacing = 6 };
            foreach (var p in problems.Take(25)) s.Children.Add(Widgets.Text("•  " + p, 12.5, t, sub: true));
            if (problems.Count > 25) s.Children.Add(Widgets.Text($"… et {problems.Count - 25} autre(s), voir le journal (Santé).", 12.5, t, sub: true));
            await Dialog("Compte rendu", new ScrollViewer { Content = s, MaxHeight = 380 }, "OK", "").ShowAsync();
        }
    }

    static string Short(string m) { m = m.ReplaceLineEndings(" ").Trim(); return m.Length > 160 ? m[..160] + "…" : m; }

    // ---------- profils ----------
    void ProfileMenu(MenuFlyout menu)
    {
        menu.Items.Clear();
        foreach (var p in Profiles.Builtin)
        {
            var name = p; var mi = new MenuFlyoutItem { Text = "Présélectionner : " + name };
            mi.Click += (_, _) => Preselect("Profil " + name, Profiles.Select(name, _all));
            menu.Items.Add(mi);
        }
        var custom = Profiles.LoadCustom().Keys.Order().ToList();
        if (custom.Count > 0) menu.Items.Add(new MenuFlyoutSeparator());
        foreach (var c in custom)
        {
            var name = c; var mi = new MenuFlyoutItem { Text = "Mon profil : " + name };
            mi.Click += (_, _) => Preselect("Profil « " + name + " »", Profiles.SelectCustom(name, _all));
            menu.Items.Add(mi);
        }
        menu.Items.Add(new MenuFlyoutSeparator());
        var save = new MenuFlyoutItem { Text = "Enregistrer la sélection actuelle comme profil…" };
        save.Click += async (_, _) => { try { await SaveProfile(); } catch (Exception ex) { Log.Error(ex.ToString()); } };
        menu.Items.Add(save);
        if (custom.Count > 0)
        {
            var del = new MenuFlyoutSubItem { Text = "Supprimer un profil" };
            foreach (var c in custom)
            {
                var name = c; var mi = new MenuFlyoutItem { Text = name };
                mi.Click += (_, _) => { Profiles.DeleteCustom(name); Status.Text = $"Profil « {name} » supprimé."; };
                del.Items.Add(mi);
            }
            menu.Items.Add(del);
        }
    }

    /// <summary>Enregistre l'état voulu (actifs + en attente, moins les désactivations en attente) sous un nom.</summary>
    async Task SaveProfile()
    {
        var box = new TextBox { PlaceholderText = "Nom du profil (ex. Mon PC de jeu)", MaxLength = 40 };
        if (await Dialog("Enregistrer comme profil", box, "Enregistrer", "Annuler").ShowAsync() != ContentDialogResult.Primary) return;
        var name = box.Text.Trim();
        if (name.Length == 0) { Status.Text = "Nom vide : profil non enregistré."; return; }
        var ids = _all.Where(Want).Select(t => t.Id).ToList();
        Profiles.SaveCustom(name, ids);
        Status.Text = $"Profil « {name} » enregistré ({ids.Count} réglages).";
    }

    // ---------- interrupteurs ----------
    void Toggled(object s, RoutedEventArgs e)
    {
        if (_refreshing || _busy) return;
        var sw = (Microsoft.UI.Xaml.Controls.Primitives.ToggleButton)s; SetWant((Tweak)sw.Tag, sw.IsChecked == true);
    }

    // ---------- actions d'en-tête ----------
    async void OneClick_Click(object s, RoutedEventArgs e)
    {
        try
        {
            if (_busy) return;
            _want.Clear();
            foreach (var t in Profiles.OneClick(_all).Where(t => !_engine.IsApplied(t.Id))) _want[t.Id] = true;
            UpdatePending();
            await ApplyPending("Optimiser en 1 clic", true);
        }
        catch (Exception ex) { Log.Error(ex.ToString()); }
    }

    async void UndoAll_Click(object s, RoutedEventArgs e)
    {
        try
        {
            if (_busy) return;
            _want.Clear();
            foreach (var t in _all.Where(t => _engine.IsApplied(t.Id))) _want[t.Id] = false;
            UpdatePending();
            await ApplyPending("Tout annuler", true);
        }
        catch (Exception ex) { Log.Error(ex.ToString()); }
    }

    async void Update_Click(object s, RoutedEventArgs e)
    {
        try { await CheckUpdate(true); } catch (Exception ex) { Log.Error(ex.ToString()); }
    }
}
