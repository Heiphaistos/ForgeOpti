using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
        foreach (var (k, v) in new (string, Brush)[]
        {
            ("ButtonBackground", fill), ("ButtonBackgroundPointerOver", hover), ("ButtonBackgroundPressed", press), ("ButtonBackgroundDisabled", t.B(t.Border)),
            ("ButtonForeground", fg), ("ButtonForegroundPointerOver", fg), ("ButtonForegroundPressed", fg),
            ("ButtonBorderBrush", t.BorderB), ("ButtonBorderBrushPointerOver", t.AccentB), ("ButtonBorderBrushPressed", t.AccentB),
        }) b.Resources[k] = v;
        return b;
    }

    public static ToggleSwitch Toggle(Theme t, bool on)
    {
        var s = new ToggleSwitch { IsOn = on, OnContent = "", OffContent = "", MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        foreach (var k in new[] { "ToggleSwitchFillOn", "ToggleSwitchFillOnPointerOver", "ToggleSwitchFillOnPressed", "ToggleSwitchStrokeOn", "ToggleSwitchStrokeOnPointerOver", "ToggleSwitchStrokeOnPressed" })
            s.Resources[k] = t.AccentB;
        foreach (var k in new[] { "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPointerOver", "ToggleSwitchKnobFillOnPressed" }) s.Resources[k] = t.OnAccentB;
        foreach (var k in new[] { "ToggleSwitchStrokeOff", "ToggleSwitchStrokeOffPointerOver", "ToggleSwitchStrokeOffPressed", "ToggleSwitchKnobFillOff", "ToggleSwitchKnobFillOffPointerOver", "ToggleSwitchKnobFillOffPressed" })
            s.Resources[k] = t.SubB;
        return s;
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
