# Dans la VM (session utilisateur) : prend des captures de l'appli pour plusieurs thèmes/dispositions/pages.
# Les captures sont écrites dans C:\ForgeOptiTest\shots\ (récupérées ensuite par collect.ps1).
$o = 'C:\ForgeOptiTest\shots.log'
function L($m) { "[$(Get-Date -f HH:mm:ss)] $m" | Out-File $o -Append -Encoding utf8 }
New-Item -ItemType Directory -Force C:\ForgeOptiTest\shots | Out-Null
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing, System.Windows.Forms
Add-Type @'
using System; using System.Runtime.InteropServices;
public class W { [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h); [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c); }
'@
$A = [Windows.Automation.AutomationElement]; $PC = [Windows.Automation.PropertyCondition]
# L'ecran « Vous avez presque termine la configuration » (1er logon) est ferme depuis l'HOTE (Msvm_Keyboard : Tab, Tab, Entree) pendant cette attente.
Start-Sleep 150
$settings = 'C:\ProgramData\ForgeOpti\settings.json'
New-Item -ItemType Directory -Force C:\ProgramData\ForgeOpti | Out-Null

# (theme, nav, view, page-motif UIA ou '', nom de fichier)
$plan = @(
  @('forge', 'side', 'cards', 'home', 'forge-accueil'),
  @('forge', 'side', 'cards', 'Gaming', 'forge-gaming'),
  @('ocean', 'side', 'cards', 'Interface', 'ocean-interface'),
  @('minuit', 'top', 'grid', 'Gaming', 'minuit-haut-grille'),
  @('neon', 'side', 'compact', 'Debloat apps', 'neon-compact'),
  @('papier', 'side', 'cards', 'home', 'papier-accueil'),
  @('contraste', 'top', 'cards', 'Gaming', 'contraste-haut'),
  @('ocean', 'side', 'cards', 'clean', 'ocean-nettoyage')
)
foreach ($p in $plan) {
    $theme, $nav, $view, $page, $name = $p
    @{ theme = $theme; nav = $nav; view = $view; page = $page; autoUpdate = $false } | ConvertTo-Json | Set-Content $settings -Encoding utf8
    $app = Start-Process C:\ForgeOptiApp\ForgeOpti.exe -PassThru
    $win = $null
    for ($i = 0; $i -lt 60 -and -not $win; $i++) { Start-Sleep 1; $win = $A::RootElement.FindFirst('Children', (New-Object $PC ([Windows.Automation.AutomationElement]::ProcessIdProperty), $app.Id)) }
    if (-not $win) { L "$name : fenetre introuvable"; Get-Process ForgeOpti -ErrorAction SilentlyContinue | Stop-Process -Force; continue }
    Start-Sleep 6
    $h = [IntPtr]$win.Current.NativeWindowHandle
    [W]::ShowWindow($h, 9) | Out-Null
    [W]::SetWindowPos($h, [IntPtr](-1), 0, 0, 0, 0, 0x43) | Out-Null   # TOPMOST, sans deplacer/redimensionner
    [W]::SetForegroundWindow($h) | Out-Null
    Start-Sleep 2
    $r = $win.Current.BoundingRectangle
    $bmp = New-Object Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
    $bmp.Save("C:\ForgeOptiTest\shots\$name.png", [Drawing.Imaging.ImageFormat]::Png)
    L "$name : ok $([int]$r.Width)x$([int]$r.Height)"
    Get-Process ForgeOpti -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep 2
}
New-Item C:\ForgeOptiTest\shots.done -Force | Out-Null
