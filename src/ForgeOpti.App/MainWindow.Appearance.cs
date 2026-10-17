using ForgeOpti.Core;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace ForgeOpti.App;

public sealed partial class MainWindow
{
    static readonly (string Id, string Label)[] NavModes = [("side", "Barre latérale"), ("top", "Onglets en haut")];
    static readonly (string Id, string Label)[] Views = [("cards", "Cartes détaillées"), ("grid", "Grille de cartes"), ("compact", "Liste compacte")];

    /// <summary>Applique le thème et la disposition à toute la fenêtre puis reconstruit en-tête, navigation et page.</summary>
    void ApplyAppearance(string themeId)
    {
        _theme = Theme.ById(themeId);
        var t = _theme;
        Root.RequestedTheme = t.Dark ? ElementTheme.Dark : ElementTheme.Light;
        Root.Background = t.BackB;
        Header.Background = t.SurfaceB; SideScroll.Background = t.SurfaceB; TopNavScroll.Background = t.SurfaceB; StatusBar.Background = t.SurfaceB;
        Status.Foreground = t.SubB; Busy.Foreground = t.AccentB;
        SearchBox.Background = t.CardB; SearchBox.Foreground = t.TextB; SearchBox.BorderBrush = t.BorderB;
        SearchBox.Resources["TextControlBackgroundFocused"] = t.CardB; SearchBox.Resources["TextControlBorderBrushFocused"] = t.AccentB;
        SearchBox.Resources["TextControlForegroundFocused"] = t.TextB; SearchBox.Resources["TextControlPlaceholderForeground"] = t.SubB;

        bool side = _navMode == "side";
        SideScroll.Visibility = side ? Visibility.Visible : Visibility.Collapsed;
        SideColumn.Width = side ? new GridLength(290) : new GridLength(0);
        TopNavScroll.Visibility = side ? Visibility.Collapsed : Visibility.Visible;

        BuildHeader();
        BuildNav();
        BuildPage();
    }

    void BuildHeader()
    {
        var t = _theme;
        Brand.Children.Clear();
        var logo = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = t.AccentB, Child = Widgets.Glyph("", 20, t.OnAccentB) };
        ((TextBlock)logo.Child).HorizontalAlignment = HorizontalAlignment.Center;
        var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = "ForgeOpti", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = t.TextB });
        names.Children.Add(new TextBlock { Text = $"Windows {(Build >= 22000 ? "11" : "10")} · build {Build}", FontSize = 11, Foreground = t.SubB });
        Brand.Children.Add(logo); Brand.Children.Add(names);

        Actions.Children.Clear(); _actionButtons.Clear();
        var one = Widgets.Btn("Optimiser en 1 clic", t, true, ""); one.Click += OneClick_Click;
        var undo = Widgets.Btn("Tout annuler", t, false, ""); undo.Click += UndoAll_Click;
        var look = Widgets.Btn("Apparence", t, false, ""); look.Flyout = BuildAppearanceFlyout();
        foreach (var b in new[] { one, undo }) { Actions.Children.Add(b); _actionButtons.Add(b); }
        Actions.Children.Add(look);
        _headerButtons = _actionButtons.ToList();
    }

    Flyout BuildAppearanceFlyout()
    {
        var t = _theme;
        var p = new StackPanel { Spacing = 10, Width = 330, RequestedTheme = Root.RequestedTheme };
        p.Children.Add(Widgets.Text("Apparence", 16, t, bold: true));

        p.Children.Add(Widgets.Text("Thème", 12, t, sub: true));
        var themes = new ComboBox { ItemsSource = Theme.All.Select(x => x.Name).ToList(), SelectedIndex = Array.IndexOf(Theme.All, t), HorizontalAlignment = HorizontalAlignment.Stretch };
        themes.SelectionChanged += (_, _) => Defer(() => { Settings.Set("theme", Theme.All[themes.SelectedIndex].Id); ApplyAppearance(Theme.All[themes.SelectedIndex].Id); });
        p.Children.Add(themes);

        p.Children.Add(Widgets.Text("Navigation", 12, t, sub: true));
        var nav = new ComboBox { ItemsSource = NavModes.Select(x => x.Label).ToList(), SelectedIndex = Array.FindIndex(NavModes, x => x.Id == _navMode), HorizontalAlignment = HorizontalAlignment.Stretch };
        nav.SelectionChanged += (_, _) => Defer(() => { _navMode = NavModes[nav.SelectedIndex].Id; Settings.Set("nav", _navMode); ApplyAppearance(_theme.Id); });
        p.Children.Add(nav);

        p.Children.Add(Widgets.Text("Affichage des réglages", 12, t, sub: true));
        var view = new ComboBox { ItemsSource = Views.Select(x => x.Label).ToList(), SelectedIndex = Array.FindIndex(Views, x => x.Id == _view), HorizontalAlignment = HorizontalAlignment.Stretch };
        view.SelectionChanged += (_, _) => Defer(() => { _view = Views[view.SelectedIndex].Id; Settings.Set("view", _view); BuildPage(); });
        p.Children.Add(view);

        p.Children.Add(new Border { Height = 1, Background = t.BorderB, Margin = new Thickness(0, 4, 0, 4) });
        var auto = Widgets.Check(t, "Mises à jour automatiques", Updater.Enabled);
        auto.Click += (_, _) => Updater.Enabled = auto.IsChecked == true;
        p.Children.Add(auto);
        var upd = Widgets.Btn("Rechercher une mise à jour", t, false, ""); upd.Click += Update_Click;
        p.Children.Add(upd);
        p.Children.Add(Widgets.Text($"Version {Updater.Current.ToString(3)}", 11, t, sub: true));
        return new Flyout { Content = p };
    }

    void Defer(Action a) => DispatcherQueue.TryEnqueue(() => a());

    // ---------- recherche / disposition ----------
    void Search_Changed(object s, TextChangedEventArgs e)
    {
        if (_refreshing) return;
        var q = SearchBox.Text.Trim();
        if (q.Length >= 2) { _page = Nav.Search; BuildNav(); BuildPage(); }
        else if (_page == Nav.Search) Go(Nav.Home);
    }

    int _lastColumns;
    void Main_SizeChanged(object s, SizeChangedEventArgs e)
    {
        if (_view != "grid" || _page is Nav.Home or Nav.Clean) return;
        if (GridColumns() != _lastColumns) BuildPage();
    }

    int GridColumns() => Math.Max(1, (int)((MainScroll.ActualWidth - 64) / 440));

    // ---------- navigation ----------
    void Go(string key)
    {
        _page = key;
        if (key != Nav.Search) Settings.Set("page", key);
        if (key != Nav.Search && SearchBox.Text.Length > 0) { _refreshing = true; SearchBox.Text = ""; _refreshing = false; }
        BuildNav(); BuildPage();
        MainScroll.ChangeView(null, 0, null, true);
    }

    (int Applied, int Total) Counts(string key)
    {
        var list = key == Nav.GamingAll ? _all.Where(x => x.Gaming) : _all.Where(x => x.Category == key);
        int total = 0, applied = 0;
        foreach (var x in list) { total++; if (_engine.IsApplied(x.Id)) applied++; }
        return (applied, total);
    }

    void BuildNav()
    {
        var t = _theme;
        SideNav.Children.Clear(); TopNav.Children.Clear();
        bool side = _navMode == "side";
        var host = side ? SideNav : TopNav;
        host.Children.Add(NavButton(new NavEntry(Nav.Home, "Accueil", ""), side));
        foreach (var g in Nav.Groups(_all))
        {
            if (side) host.Children.Add(new TextBlock { Text = g.Title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = t.SubB, Margin = new Thickness(12, 14, 0, 4) });
            else host.Children.Add(new Border { Width = 1, Background = t.BorderB, Margin = new Thickness(6, 6, 6, 6) });
            foreach (var e in g.Entries) host.Children.Add(NavButton(e, side));
        }
    }

    Button NavButton(NavEntry e, bool side)
    {
        var t = _theme;
        bool sel = _page == e.Key;
        var row = new Grid { ColumnSpacing = 10 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Widgets.Glyph(e.Glyph, 16, sel ? t.AccentB : t.SubB));
        var label = new TextBlock { Text = e.Label, Foreground = sel ? t.TextB : t.SubB, FontWeight = sel ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(label, 1); row.Children.Add(label);
        if (e.Key is not (Nav.Home or Nav.Clean))
        {
            var (a, n) = Counts(e.Key);
            var count = new TextBlock { Text = a > 0 ? $"{a}/{n}" : $"{n}", FontSize = 11, Foreground = a > 0 ? t.AccentB : t.SubB, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(count, 2); row.Children.Add(count);
        }
        var b = new Button
        {
            Content = row, HorizontalAlignment = side ? HorizontalAlignment.Stretch : HorizontalAlignment.Left, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = sel ? t.AccentSoftB : t.B(t.Surface, 0), BorderBrush = sel ? t.B(t.Accent, 0x66) : t.B(t.Surface, 0), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Tag = e.Key,
        };
        foreach (var (k, v) in new (string, Brush)[]
        {
            ("ButtonBackground", sel ? t.AccentSoftB : t.B(t.Surface, 0)), ("ButtonBackgroundPointerOver", t.B(t.Accent, 0x1A)), ("ButtonBackgroundPressed", t.B(t.Accent, 0x30)),
            ("ButtonBorderBrush", sel ? t.B(t.Accent, 0x66) : t.B(t.Surface, 0)), ("ButtonBorderBrushPointerOver", t.B(t.Accent, 0x40)), ("ButtonBorderBrushPressed", t.B(t.Accent, 0x66)),
            ("ButtonForeground", t.TextB), ("ButtonForegroundPointerOver", t.TextB), ("ButtonForegroundPressed", t.TextB),
        }) b.Resources[k] = v;
        b.Click += (_, _) => Go(e.Key);
        return b;
    }
}
