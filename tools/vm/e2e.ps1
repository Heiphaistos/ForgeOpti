# Dans la VM (session utilisateur, élevé) : test de bout en bout de l'APPLICATION RÉELLE via UI Automation.
# 1) installeur silencieux  2) UI : « Optimiser en 1 clic » -> « Tout annuler » -> page Nettoyage  3) désinstallation.
# ASCII uniquement dans les motifs (PowerShell 5.1 lit ce fichier en ANSI) : on cherche par jokers (*Optimiser en 1 clic*).
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
$un = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '6D4F0C1E' }
L "entree Programmes et fonctionnalites: $([bool]$un) version=$($un.GetValue('DisplayVersion'))"
L "raccourci menu Demarrer: $(Test-Path "$env:ProgramData\Microsoft\Windows\Start Menu\Programs\ForgeOpti.lnk")"

# ---- 2. UI de l'appli installee ----
$app = Start-Process "$inst\ForgeOpti.exe" -PassThru
$win = $null
for ($i = 0; $i -lt 60 -and -not $win; $i++) {
    Start-Sleep 1
    $win = $A::RootElement.FindFirst('Children', (New-Object $PC ([Windows.Automation.AutomationElement]::ProcessIdProperty), $app.Id))
}
L "fenetre UIA trouvee: $([bool]$win)"
function Find($pattern) {
    $all = $win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition)
    # D'abord un vrai bouton (Invoke possible), sinon n'importe quel element portant ce nom (texte d'etat, etc.).
    foreach ($e in $all) { $n = $e.Current.Name; if ($n -and $n -like $pattern -and $e.Current.ControlType.ProgrammaticName -match 'Button') { return $e } }
    foreach ($e in $all) { $n = $e.Current.Name; if ($n -and $n -like $pattern) { return $e } }
}
function Names { ($win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition) | ForEach-Object { $_.Current.Name } | Where-Object { $_ } | Select-Object -First 40) -join ' | ' }
function Click($e) { try { $e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke() } catch { $e.GetCurrentPattern([Windows.Automation.SelectionItemPattern]::Pattern).Select() } }
function WaitText($pattern, $sec) {
    for ($i = 0; $i -lt $sec; $i += 5) { Start-Sleep 5; $t = Find $pattern; if ($t) { return $t.Current.Name } }
    return $null
}
Start-Sleep 5
$b1 = Find '*Optimiser en 1 clic*'; $b2 = Find '*Tout annuler*'
L "boutons: 1clic=$([bool]$b1) annuler=$([bool]$b2)"
if (-not $b1) { L ("arbre UIA: " + (Names)) }

# Optimiser en 1 clic
if ($b1) { Click $b1; Start-Sleep 3 }
# Le bouton principal de la boite de dialogue s'appelle « Appliquer » comme les boutons des cartes : le dernier de l'arbre est celui de la popup.
$d = $null
foreach ($e in $win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition)) { if ($e.Current.Name -eq 'Appliquer') { $d = $e } }
L "dialogue de confirmation: $([bool]$d)"
# Le bouton principal de la boite de dialogue est celui par defaut : Entree le valide (fiable meme si l'arbre UIA melange les « Appliquer »).
try { $win.SetFocus() } catch {}
Start-Sleep 1
[Windows.Forms.SendKeys]::SendWait('{ENTER}')
$res = WaitText 'Termin* :*' 1500
L "1 clic: $res"
$sf = "$env:ProgramData\ForgeOpti\state.json"
$st = if (Test-Path $sf) { (Get-Content $sf -Raw | ConvertFrom-Json).Applied.PSObject.Properties.Name.Count } else { -1 }
L "tweaks appliques (state.json): $st ; points de restauration ForgeOpti: $((Get-ComputerRestorePoint -ErrorAction SilentlyContinue | Where-Object Description -match 'ForgeOpti' | Measure-Object).Count)"

# Tout annuler
$b2 = Find '*Tout annuler*'; if ($b2) { Click $b2 }
$res2 = WaitText 'Tout a *annul*' 1500
L "annulation: $res2"
$st2 = (Get-Content $sf -Raw | ConvertFrom-Json).Applied.PSObject.Properties.Name.Count
L "tweaks restant appliques apres annulation: $st2"

# Page Nettoyage
$nav = Find '*Nettoyage du disque*'; if ($nav) { Click $nav }; Start-Sleep 4
$go = Find '*Nettoyer la s*'
L "page nettoyage: bouton=$([bool]$go)"
if ($go) { Click $go; L ("nettoyage: " + (WaitText 'Nettoyage termin*' 600)) }

Get-Process ForgeOpti -ErrorAction SilentlyContinue | Stop-Process -Force
# ---- 3. desinstallation ----
$u = Start-Process "$inst\unins000.exe" '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -PassThru
if (-not $u.WaitForExit(180000)) { L 'unins: DELAI DEPASSE'; $u | Stop-Process -Force }
Start-Sleep 5
L "desinstallation exit=$($u.ExitCode) fichiers supprimes=$(-not (Test-Path "$inst\ForgeOpti.exe"))"
New-Item C:\ForgeOptiTest\e2e.done -Force | Out-Null
