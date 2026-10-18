using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace ForgeOpti.App;

/// <summary>Petits constructeurs de contrôles aux couleurs du thème (les états survol/appui sont redéfinis par contrôle).</summary>
static class Widgets
{
    public static readonly FontFamily Icons = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static TextBlock Text(string s, double size, Theme t, bool sub = false, bool bold = false, int lines = 0) => new()
    {
        Text = s, FontSize = size, TextWrapping = TextWrapping.Wrap, Foreground = sub ? t.SubB : t.TextB,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
        MaxLines = lines, TextTrimming = lines > 0 ? TextTrimming.CharacterEllipsis : TextTrimming.None,
    };

    public static TextBlock Glyph(string code, double size, Brush fg) => new() { Text = code, FontFamily = Icons, FontSize = size, Foreground = fg, VerticalAlignment = VerticalAlignment.Center };

    public static Button Btn(string label, Theme t, bool primary, string? glyph = null)
    {
        var fill = primary ? t.AccentB : t.CardB;
        var fg = primary ? t.OnAccentB : t.TextB;
        var hover = primary ? t.B(t.Accent, 0xDD) : t.B(t.Accent, 0x33);
        var press = primary ? t.B(t.Accent, 0xB0) : t.B(t.Accent, 0x55);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        if (glyph is not null) row.Children.Add(Glyph(glyph, 15, fg));
        row.Children.Add(new TextBlock { Text = label, Foreground = fg, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        var b = new Button { Content = row, Background = fill, Foreground = fg, BorderBrush = t.BorderB, CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 8, 14, 8) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, label); // lecteurs d'écran et tests UI Automation
        foreach (var (k, v) in new (string, Brush)[]
        {
            ("ButtonBackground", fill), ("ButtonBackgroundPointerOver", hover), ("ButtonBackgroundPressed", press), ("ButtonBackgroundDisabled", t.B(t.Border)),
            ("ButtonForeground", fg), ("ButtonForegroundPointerOver", fg), ("ButtonForegroundPressed", fg),
            ("ButtonBorderBrush", t.BorderB), ("ButtonBorderBrushPointerOver", t.AccentB), ("ButtonBorderBrushPressed", t.AccentB),
        }) b.Resources[k] = v;
        return b;
    }

    /// <summary>
    /// Interrupteur dessiné à la main sur un ToggleButton (UIA : motif Toggle, clavier, nom). Le ToggleSwitch de WinUI levait par moments
    /// « Invalid attribute value Unknown for property Stroke » à la reconstruction des pages, ce qui figeait l'interface.
    /// </summary>
    public static ToggleButton Toggle(Theme t, bool on)
    {
        var knob = new Microsoft.UI.Xaml.Shapes.Ellipse { Width = 14, Height = 14, VerticalAlignment = VerticalAlignment.Center };
        var pill = new Border { Width = 44, Height = 22, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1), Padding = new Thickness(3, 0, 3, 0), Child = knob };
        var none = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        var b = new ToggleButton
        {
            Content = pill, IsChecked = on, Padding = new Thickness(2), MinWidth = 0, MinHeight = 0, Background = none, BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
            UseSystemFocusVisuals = false, FocusVisualPrimaryThickness = new Thickness(0), FocusVisualSecondaryThickness = new Thickness(0),
        };
        foreach (var s in new[] { "", "PointerOver", "Pressed", "Checked", "CheckedPointerOver", "CheckedPressed", "Disabled", "CheckedDisabled" })
        {
            b.Resources["ToggleButtonBackground" + s] = none; b.Resources["ToggleButtonBorderBrush" + s] = none;
        }
        void Paint()
        {
            bool c = b.IsChecked == true;
            pill.Background = c ? t.AccentB : t.B(t.Border); pill.BorderBrush = c ? t.AccentB : t.SubB;
            knob.Fill = c ? t.OnAccentB : t.SubB; knob.HorizontalAlignment = c ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        }
        b.Checked += (_, _) => Paint(); b.Unchecked += (_, _) => Paint();
        Paint();
        return b;
    }

    public static CheckBox Check(Theme t, object content, bool on)
    {
        var c = new CheckBox { Content = content, IsChecked = on, Foreground = t.TextB };
        foreach (var k in new[] { "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver", "CheckBoxCheckBackgroundFillCheckedPressed", "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPointerOver", "CheckBoxCheckBackgroundStrokeCheckedPressed" })
            c.Resources[k] = t.AccentB;
        foreach (var k in new[] { "CheckBoxCheckGlyphForegroundChecked", "CheckBoxCheckGlyphForegroundCheckedPointerOver", "CheckBoxCheckGlyphForegroundCheckedPressed" }) c.Resources[k] = t.OnAccentB;
        foreach (var k in new[] { "CheckBoxForegroundUnchecked", "CheckBoxForegroundChecked", "CheckBoxForegroundUncheckedPointerOver", "CheckBoxForegroundCheckedPointerOver" }) c.Resources[k] = t.TextB;
        return c;
    }

    /// <summary>Pastille colorée (risque, « Redémarrage », « Gaming »…).</summary>
    public static Border Badge(string label, string colorHex, Theme t) => new()
    {
        Background = t.B(colorHex, 0x2A), BorderBrush = t.B(colorHex, 0x80), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(20),
        Padding = new Thickness(8, 1, 8, 2),
        Child = new TextBlock { Text = label, FontSize = 11, Foreground = t.B(colorHex), FontWeight = FontWeights.SemiBold },
    };

    public static Border Card(Theme t, UIElement child, double radius = 10) => new()
    {
        Background = t.CardB, BorderBrush = t.BorderB, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius), Padding = new Thickness(14, 10, 14, 10), Child = child,
    };
}
