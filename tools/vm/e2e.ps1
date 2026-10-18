# Dans la VM (session utilisateur, eleve) : test de bout en bout de l'APPLICATION REELLE via UI Automation (0.5.0 : selection puis application).
# 1) installeur silencieux  2) selection differee  3) persistance (--repair)  4) profils  5) 1 clic + reglages proteges  6) Tout annuler  7) desinstallation.
# ASCII uniquement dans les motifs (PowerShell 5.1 lit ce fichier en ANSI) : on cherche par jokers.
$o = 'C:\ForgeOptiTest\e2e.log'
function L($m) { "[$(Get-Date -f HH:mm:ss)] $m" | Out-File $o -Append -Encoding utf8 }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms
$A = [Windows.Automation.AutomationElement]; $PC = [Windows.Automation.PropertyCondition]

# ---- 1. installeur ----
$setup = Get-ChildItem C:\ForgeOptiTest\ForgeOpti-Setup-*.exe | Select-Object -First 1
$p = Start-Process $setup.FullName '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /LOG=C:\ForgeOptiTest\setup.log' -PassThru
if (-not $p.WaitForExit(300000)) { L 'setup: DELAI DEPASSE'; $p | Stop-Process -Force }
$inst = "$env:ProgramFiles\ForgeOpti"
L "setup exit=$($p.ExitCode) exe=$(Test-Path "$inst\ForgeOpti.exe") unins=$(Test-Path "$inst\unins000.exe")"

$sf = "$env:ProgramData\ForgeOpti\state.json"
function Applied { if (Test-Path $sf) { @((Get-Content $sf -Raw | ConvertFrom-Json).Applied.PSObject.Properties.Name) } else { @() } }
function Reg($k, $n) { try { (Get-ItemProperty $k -Name $n -ErrorAction Stop).$n } catch { 'absent' } }
$cab = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\CabinetState'
$svcBefore = @{}; foreach ($s in 'Spooler', 'BthServ', 'SSDPSrv', 'upnphost', 'wuauserv') { $svcBefore[$s] = Reg "HKLM:\SYSTEM\CurrentControlSet\Services\$s" 'Start' }
L ("services avant: " + (($svcBefore.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ' '))
$fullBefore = Reg $cab 'FullPath'; L "FullPath avant=$fullBefore"

# ---- 2. UI de l'appli installee ----
$app = Start-Process "$inst\ForgeOpti.exe" -PassThru
$win = $null
for ($i = 0; $i -lt 60 -and -not $win; $i++) {
    Start-Sleep 1
    $win = $A::RootElement.FindFirst('Children', (New-Object $PC ([Windows.Automation.AutomationElement]::ProcessIdProperty), $app.Id))
}
L "fenetre UIA trouvee: $([bool]$win)"
function All { $win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition) }
function Find($pattern) {
    $all = All
    foreach ($e in $all) { $n = $e.Current.Name; if ($n -and $n -like $pattern -and $e.Current.ControlType.ProgrammaticName -match 'Button') { return $e } }
    foreach ($e in $all) { $n = $e.Current.Name; if ($n -and $n -like $pattern) { return $e } }
}
function Names { (All | ForEach-Object { $_.Current.Name } | Where-Object { $_ } | Select-Object -First 50) -join ' | ' }
function Click($e) { try { $e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { $e.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() } }
function WaitText($pattern, $sec) { for ($i = 0; $i -lt $sec; $i += 5) { Start-Sleep 5; $t = Find $pattern; if ($t) { return $t.Current.Name } }; return $null }
function Confirm { $b = $null; foreach ($e in (All)) { if ($e.Current.Name -eq 'Appliquer') { $b = $e } }; if ($b) { Click $b } else { Enter } }
function Enter { try { $win.SetFocus() } catch {}; Start-Sleep 1; [Windows.Forms.SendKeys]::SendWait('{ENTER}') }
Start-Sleep 8
L ("arbre UIA: " + (Names))

# ---- 3. selection differee : un interrupteur ne doit RIEN changer avant validation ----
$nav = Find 'Explorateur'; if ($nav) { Click $nav }; Start-Sleep 4
$tg = Find '*chemin complet*'
L "interrupteur chemin complet trouve: $([bool]$tg)"
if ($tg) {
    $tg.GetCurrentPattern([Windows.Automation.TogglePattern]::Pattern).Toggle(); Start-Sleep 3
    L "apres bascule : FullPath=$(Reg $cab 'FullPath') (attendu inchange=$fullBefore) ; applique dans state.json=$((Applied) -contains 'exp.chemin-complet') (attendu False)"
    $bar = Find '*modification*attente*'; L "barre d'attente: $(if ($bar) { $bar.Current.Name } else { 'ABSENTE' })"
    $ap = Find '*Voir et appliquer*'; if ($ap) { Click $ap; Start-Sleep 3; Confirm }
    L ("application: " + (WaitText 'Termin* :*' 300))
    L "apres validation : FullPath=$(Reg $cab 'FullPath') (attendu 1) ; state.json=$((Applied) -contains 'exp.chemin-complet') (attendu True)"
}

# ---- 4. persistance : une 'mise a jour Windows' defait la valeur, --repair la remet ----
Set-ItemProperty $cab FullPath 0 -Type DWord
L "valeur defaite a la main : FullPath=$(Reg $cab 'FullPath')"
$r = Start-Process "$inst\ForgeOpti.exe" '--repair' -PassThru
if (-not $r.WaitForExit(120000)) { L '--repair: DELAI DEPASSE'; $r | Stop-Process -Force }
L "apres --repair : FullPath=$(Reg $cab 'FullPath') (attendu 1) exit=$($r.ExitCode)"
schtasks /Query /TN ForgeOpti-Verification 2>&1 | Out-String | ForEach-Object { L "tache planifiee: $(($_ -split "`n" | Select-Object -Last 3) -join ' / ')" }
L ("journal: " + ((Get-Content "$env:ProgramData\ForgeOpti\forgeopti.log" -Tail 4) -join ' || '))

# ---- 4b. relance : une page avec un reglage ACTIF doit s'afficher sans exception ----
function ErrCount { (Select-String -Path "$env:ProgramData\ForgeOpti\forgeopti.log" -Pattern '\[ERROR\]' -ErrorAction SilentlyContinue | Measure-Object).Count }
Stop-Process -Id $app.Id -Force -ErrorAction SilentlyContinue; Start-Sleep 3
$e0 = ErrCount
$app = Start-Process "$inst\ForgeOpti.exe" -PassThru
$win = $null
for ($i = 0; $i -lt 60 -and -not $win; $i++) { Start-Sleep 1; $win = $A::RootElement.FindFirst('Children', (New-Object $PC ([Windows.Automation.AutomationElement]::ProcessIdProperty), $app.Id)) }
Start-Sleep 8
$nav = Find 'Explorateur'; if ($nav) { Click $nav }; Start-Sleep 8
L "relance + page Explorateur (reglage actif) : nouvelles erreurs journal = $((ErrCount) - $e0)"

# ---- 5. profils : preselection sans application ----
$before = (Applied).Count
$pb = Find 'Profils'; if ($pb) { Click $pb }
$mi = $null; for ($i = 0; $i -lt 6 -and -not $mi; $i++) { Start-Sleep 2; $mi = Find '*Bureautique*' }
L "menu profils: entree Bureautique trouvee=$([bool]$mi)"
if ($mi) {
    Click $mi; Start-Sleep 4
    $bar = Find '*modification*attente*'; L "profil Bureautique -> $(if ($bar) { $bar.Current.Name } else { 'ABSENTE' }) ; applique inchange=$((Applied).Count -eq $before)"
    $ca = Find '*Annuler la s*'; if ($ca) { Click $ca; Start-Sleep 2 }
    L "barre apres annulation de la selection: $(if (Find '*modification*attente*') { 'ENCORE LA' } else { 'disparue' })"
} else { [Windows.Forms.SendKeys]::SendWait('{ESC}'); Start-Sleep 1 }

# ---- 6. 1 clic : reglages vitaux intacts ----
$b1 = Find '*Optimiser en 1 clic*'; if ($b1) { Click $b1; Start-Sleep 3; Confirm; Start-Sleep 15 }
L ("1 clic: " + (WaitText 'Termin* :*' 1500))
$ids = Applied
L "tweaks appliques: $($ids.Count) ; points de restauration ForgeOpti: $((Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Where-Object Description -match 'ForgeOpti' | Measure-Object).Count)"
$vitaux = $ids | Where-Object { $_ -match 'spooler|bluetooth|ssdp|lltd|decouverte|faxscan|printing' }
L "reglages vitaux appliques (attendu aucun): $(if ($vitaux) { $vitaux -join ',' } else { 'aucun' })"
foreach ($s in 'Spooler', 'BthServ', 'SSDPSrv', 'upnphost', 'wuauserv') { $now = Reg "HKLM:\SYSTEM\CurrentControlSet\Services\$s" 'Start'; L "service $s Start: $($svcBefore[$s]) -> $now $(if ($now -ne $svcBefore[$s]) { '<<< CHANGE' })" }

# ---- 7. tout annuler (apercu puis confirmation) ----
$b2 = Find '*Tout annuler*'; if ($b2) { Click $b2; Start-Sleep 3; Confirm; Start-Sleep 15 }
L ("annulation: " + (WaitText 'Termin* :*' 1500))
L "tweaks restant appliques apres annulation: $((Applied).Count) ; FullPath=$(Reg $cab 'FullPath') (attendu $fullBefore)"

# Page Sante + Nettoyage
$nav = Find '*Sant* et persistance*'; if ($nav) { Click $nav }; Start-Sleep 3
L "page sante: bouton verifier=$([bool](Find '*rifier et r*parer*'))"
$nav = Find '*Nettoyage du disque*'; if ($nav) { Click $nav }; Start-Sleep 4
$go = Find '*Nettoyer la s*'
L "page nettoyage: bouton=$([bool]$go)"
if ($go) { Click $go; L ("nettoyage: " + (WaitText 'Nettoyage termin*' 600)) }

Get-Process ForgeOpti -ErrorAction SilentlyContinue | Stop-Process -Force
# ---- 8. desinstallation ----
$u = Start-Process "$inst\unins000.exe" '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru
if (-not $u.WaitForExit(180000)) { L 'unins: DELAI DEPASSE'; $u | Stop-Process -Force }
Start-Sleep 5
schtasks /Query /TN ForgeOpti-Verification 2>$null | Out-Null
L "desinstallation exit=$($u.ExitCode) fichiers supprimes=$(-not (Test-Path "$inst\ForgeOpti.exe")) tache supprimee=$($LASTEXITCODE -ne 0)"
Copy-Item "$env:ProgramData\ForgeOpti\forgeopti.log" C:\ForgeOptiTest\forgeopti.log -Force -ErrorAction SilentlyContinue
New-Item C:\ForgeOptiTest\e2e.done -Force | Out-Null
