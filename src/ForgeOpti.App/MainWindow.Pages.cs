using ForgeOpti.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ForgeOpti.App;

public sealed partial class MainWindow
{
    void BuildPage()
    {
        Main.Children.Clear(); _switches.Clear();
        switch (_page)
        {
            case Nav.Home: HomePage(); break;
            case Nav.Clean: CleanerPage(); break;
            case Nav.Health: HealthPage(); break;
            case Nav.Search: SearchPage(); break;
            default: TweakPage(_page); break;
        }
    }

    // ---------- éléments communs ----------
    void PageTitle(string title, string subtitle, string glyph)
    {
        var t = _theme;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Margin = new Thickness(0, 0, 0, 6) };
        row.Children.Add(new Border { Width = 44, Height = 44, CornerRadius = new CornerRadius(12), Background = t.AccentSoftB, Child = Widgets.Glyph(glyph, 22, t.AccentB) });
        ((TextBlock)((Border)row.Children[0]).Child).HorizontalAlignment = HorizontalAlignment.Center;
        var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        txt.Children.Add(Widgets.Text(title, 24, t, bold: true));
        _subtitle = Widgets.Text(subtitle, 13, t, sub: true);
        txt.Children.Add(_subtitle);
        row.Children.Add(txt);
        Main.Children.Add(row);
    }

    void Heading(string text, string colorHex)
    {
        var t = _theme;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 14, 0, 2) };
        row.Children.Add(new Border { Width = 4, Height = 16, CornerRadius = new CornerRadius(2), Background = t.B(colorHex) });
        row.Children.Add(Widgets.Text(text, 13, t, bold: true));
        Main.Children.Add(row);
    }

    // ---------- accueil ----------
    void HomePage()
    {
        var t = _theme;
        int applied = _all.Count(x => _engine.IsApplied(x.Id));
        PageTitle("Bienvenue dans ForgeOpti", $"{_all.Count} réglages disponibles sur ce PC · {applied} actifs · tout est réversible", "");

        var wrap = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = 400, ItemHeight = 170, MaximumRowsOrColumns = -1 };
        wrap.Children.Add(HomeCard("", "Optimisation en 1 clic", $"{Profiles.OneClick(_all).Count(t => !_engine.IsApplied(t.Id))} réglages sûrs, après aperçu et point de restauration automatique. Impression, Bluetooth, réseau local et mises à jour restent intacts.", "Optimiser", true,
            () => { OneClick_Click(this, null!); return Task.CompletedTask; }));
        wrap.Children.Add(HomeCard("", "Profil Gaming", "Moins de latence et plus d'images par seconde : jeux, GPU, alimentation, réseau.", "Présélectionner", false,
            () => { Preselect("Profil Gaming", Profiles.Select(Profiles.Gaming, _all)); return Task.CompletedTask; }));
        wrap.Children.Add(HomeCard("", "Profil Bureautique", "PC de travail sobre : télémétrie et publicités réduites, aucun réglage de jeu. Imprimantes et périphériques intacts.", "Présélectionner", false,
            () => { Preselect("Profil Bureautique", Profiles.Select(Profiles.Bureautique, _all)); return Task.CompletedTask; }));
        wrap.Children.Add(HomeCard("", "Profil Hybride", "Bureau propre et gains de jeu à la fois : le mélange Bureautique + Gaming.", "Présélectionner", false,
            () => { Preselect("Profil Hybride", Profiles.Select(Profiles.Hybride, _all)); return Task.CompletedTask; }));
        wrap.Children.Add(HomeCard("", "Profil Confidentialité", "Télémétrie, publicités, suggestions et Copilot réduits, sans toucher à la sécurité.", "Présélectionner", false,
            () => { Preselect("Profil Confidentialité", Profiles.Select(Profiles.Confidentialite, _all)); return Task.CompletedTask; }));
        wrap.Children.Add(HomeCard("", "Nettoyage du disque", "Caches, temporaires, journaux et caches de jeux : libère de la place en un clic.", "Ouvrir", false,
            () => { Go(Nav.Clean); return Task.CompletedTask; }));
        Main.Children.Add(wrap);

        Heading("Comment ça marche", t.Accent);
        foreach (var line in new[]
        {
            "Les interrupteurs ne changent rien tout de suite : ils préparent une sélection. Une barre en bas t'indique combien de changements attendent, puis « Voir et appliquer » affiche la liste exacte avant de valider.",
            "La valeur d'origine de chaque réglage est mémorisée : le désactiver remet exactement l'état précédent.",
            "Les profils présélectionnent seulement : tu peux tout ajuster avant d'appliquer, et enregistrer ta propre sélection (bouton Profils).",
            "Jamais touchés par les profils ni par le 1 clic : impression, Bluetooth, découverte réseau et partages, mises à jour et protection de Windows.",
            "Un point de restauration Windows est créé avant le premier changement de la session.",
            "Les pastilles indiquent le risque : Sûr (vert), Modéré (orange), Avancé (rouge, jamais inclus dans le 1 clic).",
            "Astuce : la recherche en haut trouve un réglage par son nom ou sa description, dans toutes les catégories.",
        }) Main.Children.Add(Widgets.Text("•  " + line, 13, t, sub: true));
    }

    Border HomeCard(string glyph, string title, string text, string action, bool primary, Func<Task> run)
    {
        var t = _theme;
        var s = new StackPanel { Spacing = 8 };
        var head = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        head.Children.Add(Widgets.Glyph(glyph, 22, t.AccentB));
        head.Children.Add(Widgets.Text(title, 16, t, bold: true));
        s.Children.Add(head);
        s.Children.Add(Widgets.Text(text, 12.5, t, sub: true, lines: 3));
        var b = Widgets.Btn(action, t, primary);
        b.Click += async (_, _) => await run();
        b.Margin = new Thickness(0, 6, 0, 0);
        s.Children.Add(b);
        var c = Widgets.Card(t, s, 14); c.Margin = new Thickness(0, 0, 14, 14); c.Padding = new Thickness(18, 14, 18, 14);
        return c;
    }

    // ---------- page de réglages ----------
    List<Tweak> ListFor(string key) => key == Nav.GamingAll ? _all.Where(x => x.Gaming).ToList() : _all.Where(x => x.Category == key).ToList();

    void TweakPage(string key)
    {
        var t = _theme;
        var list = ListFor(key);
        var (applied, total) = Counts(key);
        var entry = Nav.Groups(_all).SelectMany(g => g.Entries).FirstOrDefault(e => e.Key == key);
        PageTitle(entry?.Label ?? key, $"{total} réglages · {applied} actifs", entry?.Glyph ?? "");

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 4) };
        var safeTodo = list.Where(x => x.Risk == Risk.Sur && !Profiles.Protected(x) && Want(x) == false).ToList();
        var on = Widgets.Btn($"Sélectionner les réglages sûrs ({safeTodo.Count})", t, true);
        on.Click += (_, _) => Preselect($"{entry?.Label ?? key}", safeTodo);
        var off = Widgets.Btn("Tout désactiver ici", t, false);
        off.Click += (_, _) => Preselect($"{entry?.Label ?? key}", list.Where(x => Want(x)), on: false);
        // Suppression d'applications, d'Edge/OneDrive et de fonctionnalités Windows : jamais en masse, choix un par un.
        bool bulk = key is not ("Debloat apps" or "Edge et OneDrive" or "Fonctionnalités Windows");
        if (bulk) bar.Children.Add(on);
        bar.Children.Add(off);
        Main.Children.Add(bar);

        Items(list);
    }

    void SearchPage()
    {
        var t = _theme;
        var q = SearchBox.Text.Trim();
        var hits = _all.Where(x => x.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase) || x.Description.Contains(q, StringComparison.CurrentCultureIgnoreCase) || x.Id.Contains(q, StringComparison.OrdinalIgnoreCase)).Take(150).ToList();
        PageTitle($"Résultats pour « {q} »", $"{hits.Count} réglage(s) trouvé(s) dans toutes les catégories", "");
        if (hits.Count == 0) Main.Children.Add(Widgets.Text("Aucun résultat. Essaie un autre mot (ex. « widgets », « souris », « Edge »).", 13, t, sub: true));
        Items(hits, showCategory: true);
    }

    /// <summary>Affiche les réglages regroupés par niveau de risque, dans l'affichage choisi (cartes, grille, compact).</summary>
    void Items(List<Tweak> list, bool showCategory = false)
    {
        var t = _theme;
        _lastColumns = GridColumns();
        foreach (var (risk, label) in new[] { (Risk.Sur, "Réglages sûrs"), (Risk.Modere, "Réglages modérés : effet visible ou à connaître"), (Risk.Avance, "Réglages avancés : à utiliser en connaissance de cause") })
        {
            var part = list.Where(x => x.Risk == risk).OrderBy(x => x.Name).ToList();
            if (part.Count == 0) continue;
            Heading($"{label} ({part.Count})", t.RiskColor(risk));
            if (_view == "grid")
            {
                var w = Math.Max(300, (MainScroll.ActualWidth - 64 - 12 * (_lastColumns - 1)) / _lastColumns);
                var g = new VariableSizedWrapGrid { Orientation = Orientation.Horizontal, ItemWidth = w + 12, ItemHeight = 150, MaximumRowsOrColumns = -1 };
                foreach (var x in part) { var c = TweakCard(x, showCategory); c.Margin = new Thickness(0, 0, 12, 12); c.Height = 138; g.Children.Add(c); }
                Main.Children.Add(g);
            }
            else foreach (var x in part) Main.Children.Add(TweakCard(x, showCategory));
        }
    }

    Border TweakCard(Tweak x, bool showCategory)
    {
        var t = _theme;
        bool compact = _view == "compact";
        var sw = Widgets.Toggle(t, Want(x)); sw.Tag = x; sw.Checked += Toggled; sw.Unchecked += Toggled; _switches.Add(sw);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(sw, x.Name);

        var badges = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        badges.Children.Add(Widgets.Badge(x.Risk switch { Risk.Sur => "Sûr", Risk.Modere => "Modéré", _ => "Avancé" }, t.RiskColor(x.Risk), t));
        if (x.OneClick) badges.Children.Add(Widgets.Badge("1 clic", t.Accent, t));
        if (Profiles.Protected(x)) badges.Children.Add(Widgets.Badge("Protégé des profils", t.Sub, t));
        if (x.Reboot) badges.Children.Add(Widgets.Badge("Redémarrage", t.Sub, t));
        if (x.Gaming) badges.Children.Add(Widgets.Badge("Gaming", t.Accent, t));
        if (x.MinBuild >= 22000) badges.Children.Add(Widgets.Badge("Windows 11", t.Sub, t));
        if (x.MaxBuild is > 0 and < 22000) badges.Children.Add(Widgets.Badge("Windows 10", t.Sub, t));
        if (showCategory) badges.Children.Add(Widgets.Badge(x.Category, t.Sub, t));

        var left = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(Widgets.Text(x.Name, compact ? 13.5 : 14.5, t, bold: true, lines: _view == "grid" ? 2 : 0));
        if (!compact) left.Children.Add(Widgets.Text(x.Description, 12.5, t, sub: true, lines: _view == "grid" ? 3 : 0));
        left.Children.Add(badges);

        var grid = new Grid { ColumnSpacing = 14 };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(left); Grid.SetColumn(sw, 1); grid.Children.Add(sw);
        var card = Widgets.Card(t, grid);
        if (compact) { card.Padding = new Thickness(14, 6, 14, 6); ToolTipService.SetToolTip(card, x.Description); }
        return card;
    }

    // ---------- santé et persistance ----------
    void HealthPage()
    {
        var t = _theme;
        PageTitle("Santé et persistance", "Vérifie que tes réglages sont toujours en place après les mises à jour de Windows.", "");
        int applied = _all.Count(x => _engine.IsApplied(x.Id));
        Main.Children.Add(Widgets.Text($"{applied} réglage(s) actif(s). Les valeurs sont écrites dans le registre ; si une mise à jour de Windows en défait certaines, ForgeOpti les remet en place.", 13, t, sub: true));
        Main.Children.Add(Widgets.Text("Dernière vérification : " + Settings.Get("lastCheck", "jamais"), 13, t));

        var auto = Widgets.Check(t, "Vérifier automatiquement à l'ouverture de session Windows et au lancement de ForgeOpti", Maintenance.AutoRepair);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(auto, "Vérification automatique");
        auto.Click += async (_, _) =>
        {
            Maintenance.AutoRepair = auto.IsChecked == true;
            await Task.Run(() => Maintenance.SyncTask(Maintenance.AutoRepair && _all.Any(x => _engine.IsApplied(x.Id))));
            Status.Text = Maintenance.AutoRepair ? "Vérification automatique activée." : "Vérification automatique désactivée.";
        };
        Main.Children.Add(Widgets.Card(t, auto));

        var now = Widgets.Btn("Vérifier et réparer maintenant", t, true); now.HorizontalAlignment = HorizontalAlignment.Left;
        now.Click += async (_, _) =>
        {
            if (_busy) return;
            await Run(async () =>
            {
                Status.Text = "Vérification des réglages appliqués…";
                var res = await Task.Run(() => Maintenance.Repair(_engine, _all));
                int ok = res.Count(r => r.Error is null);
                Status.Text = res.Count == 0 ? "Tout est en place : aucun réglage n'a été défait." : $"{ok} réglage(s) remis en place" + (ok < res.Count ? $", {res.Count - ok} en échec (voir le journal)." : ".");
            }, () => BuildPage());
        };
        Main.Children.Add(now);

        Heading("Journal récent", t.Accent);
        var lines = Log.Tail(30);
        if (lines.Count == 0) Main.Children.Add(Widgets.Text("Aucune entrée pour le moment.", 12.5, t, sub: true));
        foreach (var l in lines) Main.Children.Add(Widgets.Text(l, 11.5, t, sub: true));
    }

    // ---------- nettoyage ----------
    void CleanerPage()
    {
        var t = _theme;
        PageTitle("Nettoyage du disque", "Caches et fichiers temporaires que Windows recrée. Ce nettoyage n'est pas annulable.", "");
        var optIn = new HashSet<string> { "composants", "prefetch", "cache-polices", "jump-lists", "shaders-nvidia", "shaders-amd", "shaders-directx" };
        var boxes = new List<(CheckBox Box, CleanItem Item, TextBlock Size)>();
        foreach (var it in Cleaner.Items.Concat(CleanerExtra.Items))
        {
            var size = new TextBlock { Text = "…", Foreground = t.SubB, VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5 };
            var body = new StackPanel { Spacing = 2 };
            body.Children.Add(Widgets.Text(it.Name, 14, t, bold: true));
            body.Children.Add(Widgets.Text(it.Description + (optIn.Contains(it.Id) ? "  (non coché par défaut)" : ""), 12, t, sub: true));
            var cb = Widgets.Check(t, body, !optIn.Contains(it.Id));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(cb, it.Name);
            var g = new Grid { ColumnSpacing = 12 };
            g.ColumnDefinitions.Add(new ColumnDefinition()); g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(cb); Grid.SetColumn(size, 1); g.Children.Add(size);
            Main.Children.Add(Widgets.Card(t, g)); boxes.Add((cb, it, size));
            _ = Task.Run(() => Cleaner.Measure(it)).ContinueWith(r => DispatcherQueue.TryEnqueue(() => size.Text = it.Special is not null && !it.Dirs().Any() ? "" : Human(r.Result)));
        }
        var go = Widgets.Btn("Nettoyer la sélection", t, true, ""); go.Margin = new Thickness(0, 8, 0, 0); go.HorizontalAlignment = HorizontalAlignment.Left;
        go.Click += async (_, _) =>
        {
            if (_busy) return;
            await Run(async () =>
            {
                long freed = 0;
                foreach (var (box, item, size) in boxes.Where(b => b.Box.IsChecked == true))
                {
                    Status.Text = $"Nettoyage : {item.Name}…";
                    freed += await Task.Run(() => Cleaner.Clean(item));
                    size.Text = Human(await Task.Run(() => Cleaner.Measure(item)));
                }
                Status.Text = $"Nettoyage terminé : {Human(freed)} libérés.";
            }, () => { });
        };
        Main.Children.Add(go);
    }

    static string Human(long b) => b >= 1L << 30 ? $"{b / (double)(1L << 30):0.0} Go" : b >= 1L << 20 ? $"{b / (double)(1L << 20):0} Mo" : b > 0 ? $"{b / 1024} Ko" : "0";
}
