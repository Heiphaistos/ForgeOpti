# Dans la VM : lance l'appli et la laisse ouverte 6 minutes (captures d'écran faites depuis l'hôte avec shot.ps1).
$o = 'C:\ForgeOptiTest\ui.log'
function L($m) { "[$(Get-Date -f HH:mm:ss)] $m" | Out-File $o -Append -Encoding utf8 }
Start-Process C:\ForgeOptiApp\ForgeOpti.exe
Start-Sleep 15
$p = Get-Process ForgeOpti -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
L "fenetre: $([bool]$p) titre='$($p.MainWindowTitle)' mem=$([math]::Round($p.WorkingSet64/1MB)) Mo"
Start-Sleep 360
Get-Process ForgeOpti -ErrorAction SilentlyContinue | Stop-Process -Force
New-Item C:\ForgeOptiTest\ui.done -Force | Out-Null
