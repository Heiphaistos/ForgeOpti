# Dans la VM (session utilisateur, élevé) : réinstallations réelles (winget/Store) avec délai maximal.
$o = 'C:\ForgeOptiTest\suite2.log'
function L($m) { "[$(Get-Date -f HH:mm:ss)] $m" | Out-File $o -Append -Encoding utf8 }
L "date=$(Get-Date) user=$env:USERNAME (suite reinstallations)"
Start-Service w32time -ErrorAction SilentlyContinue
w32tm /resync /force 2>&1 | Out-Null
$t = 'C:\ForgeOptiTest\ForgeOpti.VmTest.exe'
$p2 = Start-Process $t 'C:\ForgeOptiTest only ui.widgets,app.solitaire,app.cortana,app.teams-pro,edge.desinstaller' -PassThru
if (-not $p2.WaitForExit(1200000)) { L 'only: DELAI DEPASSE (1200 s)'; $p2 | Stop-Process -Force }
L "only-report ecrit: $(Test-Path C:\ForgeOptiTest\only-report.txt)"
New-Item C:\ForgeOptiTest\suite2.done -Force | Out-Null
