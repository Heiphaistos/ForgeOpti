using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ForgeOpti.App;

/// <summary>Une esthétique complète : fond, surfaces, texte, accent et couleurs de risque.</summary>
public sealed record Theme(string Id, string Name, bool Dark, string Back, string Surface, string Card, string Border,
    string Text, string Sub, string Accent, string OnAccent, string Good, string Warn, string Bad)
{
    public static readonly Theme[] All =
    [
        new("forge", "Forge (sombre, orange)", true, "#15171C", "#1B1E25", "#22262F", "#2E333E", "#ECEEF2", "#9AA3B2", "#FF8A1F", "#1A1205", "#3DDC84", "#FFB020", "#FF5C5C"),
        new("ocean", "Océan (clair, bleu)", false, "#EEF3F9", "#FFFFFF", "#FFFFFF", "#D5DEEA", "#17212F", "#5B6B80", "#1F6FEB", "#FFFFFF", "#1A9F5B", "#C77700", "#D13B3B"),
        new("minuit", "Minuit (bleu nuit, cyan)", true, "#0B1220", "#0F1A2E", "#13223B", "#1F3354", "#E6EEFB", "#8FA6C8", "#22D3EE", "#04202A", "#34D399", "#FBBF24", "#F87171"),
        new("neon", "Néon (noir, magenta)", true, "#0A0A0F", "#11111A", "#171724", "#2A2A44", "#F2F2FF", "#A09CC8", "#E040FB", "#1A0620", "#39FF88", "#FFD23F", "#FF4D6D"),
        new("papier", "Papier (clair, terracotta)", false, "#F6F1E7", "#FBF8F1", "#FFFFFF", "#E3DAC8", "#2A2418", "#7A6E58", "#B4532A", "#FFFFFF", "#2F7D32", "#A86400", "#B3261E"),
        new("contraste", "Contraste élevé (noir, jaune)", true, "#000000", "#000000", "#0A0A0A", "#FFFFFF", "#FFFFFF", "#E0E0E0", "#FFE600", "#000000", "#00FF7F", "#FFE600", "#FF5050"),
    ];

    public static Theme ById(string id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];

    public static Color C(string hex, byte alpha = 255)
    {
        hex = hex.TrimStart('#');
        return Color.FromArgb(alpha, Convert.ToByte(hex[..2], 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));
    }

    public SolidColorBrush B(string hex, byte alpha = 255) => new(C(hex, alpha));

    public SolidColorBrush BackB => B(Back);
    public SolidColorBrush SurfaceB => B(Surface);
    public SolidColorBrush CardB => B(Card);
    public SolidColorBrush BorderB => B(Border);
    public SolidColorBrush TextB => B(Text);
    public SolidColorBrush SubB => B(Sub);
    public SolidColorBrush AccentB => B(Accent);
    public SolidColorBrush OnAccentB => B(OnAccent);
    public SolidColorBrush AccentSoftB => B(Accent, 0x26);

    public string RiskColor(ForgeOpti.Core.Risk r) => r switch { ForgeOpti.Core.Risk.Sur => Good, ForgeOpti.Core.Risk.Modere => Warn, _ => Bad };
}
