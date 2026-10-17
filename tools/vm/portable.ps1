# Dans la VM (session utilisateur) : la version portable doit démarrer directement, sans assistant d'installation.
$o = 'C:\ForgeOptiTest\portable.log'
function L($m) { "[$(Get-Date -f HH:mm:ss)] $m" | Out-File $o -Append -Encoding utf8 }
$exe = 'C:\ForgeOptiTest\ForgeOpti-Portable.exe'
L "taille=$([math]::Round((Get-Item $exe).Length/1MB,1)) Mo"
$sw = [Diagnostics.Stopwatch]::StartNew()
Start-Process $exe
$splash = ''
for ($i = 0; $i -lt 12; $i++) {
    Start-Sleep 1
    $t = (Get-Process 'ForgeOpti-Portable' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | ForEach-Object MainWindowTitle) -join ''
    if ($t) { $splash = $t; break }
}
L "fenetre d'attente 1re execution: '$splash'"
$ok = $false
for ($i = 0; $i -lt 90; $i++) {
    Start-Sleep 1
    $p = Get-Process ForgeOpti -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
    if ($p) { $ok = $true; break }
}
L "fenetre ForgeOpti visible=$ok apres $([math]::Round($sw.Elapsed.TotalSeconds,1)) s"
L ("fenetres de configuration (Setup/Install) ouvertes: " + ((Get-Process | Where-Object { $_.MainWindowTitle -match 'Install|Setup|Assistant' } | ForEach-Object { $_.ProcessName + ':' + $_.MainWindowTitle }) -join ';'))
L ("dossier extrait: " + ((Get-ChildItem "$env:LOCALAPPDATA\ForgeOpti-Portable" -ErrorAction SilentlyContinue | ForEach-Object Name) -join ','))
L ("entree Programmes (registre Uninstall) ForgeOpti: " + [bool](Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match 'ForgeOpti|6D4F0C1E' }))
Start-Sleep 20
L "toujours en cours: $((Get-Process ForgeOpti -ErrorAction SilentlyContinue | Measure-Object).Count)"
Get-Process ForgeOpti -ErrorAction SilentlyContinue | Stop-Process -Force
# 2e lancement : doit etre quasi instantane (deja extrait)
$sw.Restart(); Start-Process $exe
for ($i = 0; $i -lt 60; $i++) { Start-Sleep 1; if (Get-Process ForgeOpti -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }) { break } }
L "2e lancement: fenetre visible apres $([math]::Round($sw.Elapsed.TotalSeconds,1)) s"
Get-Process ForgeOpti -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item C:\ForgeOptiTest\portable.done -Force | Out-Null
