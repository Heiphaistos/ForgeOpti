# Prépare et démarre la VM Hyper-V de test. Usage : vmcycle.ps1 -Mode phases|suite2|portable [-Memory 3]
# Checkpoint hors-ligne (VM éteinte) -> injection du binaire + d'un service SYSTEM à usage unique -> démarrage.
param([string] $Mode = 'phases', [int]$MemoryGo = 3,
      [string]$Vm = 'W11 dev', [string]$Snapshot = 'avant-ForgeOpti-test3', [string]$Filter = '')
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$tmp = $env:TEMP
if ((Get-VM $Vm).State -ne 'Off') { Stop-VM $Vm -TurnOff -Force }
if (-not (Get-VMSnapshot -VMName $Vm -Name $Snapshot -ErrorAction SilentlyContinue)) { Checkpoint-VM -Name $Vm -SnapshotName $Snapshot }
Set-VMMemory $Vm -DynamicMemoryEnabled $true -StartupBytes ($MemoryGo * 1GB) -MinimumBytes 1GB -MaximumBytes (($MemoryGo + 1) * 1GB)
Get-VMDvdDrive -VMName $Vm | Set-VMDvdDrive -Path $null
$p = (Get-VM $Vm).HardDrives[0].Path
$d = Mount-VHD -Path $p -Passthru | Get-Disk
$L = (Get-Partition -DiskNumber $d.Number | Get-Volume | Where-Object { $_.DriveLetter -and $_.Size -gt 50GB }).DriveLetter
Remove-Item "${L}:\ForgeOptiTest", "${L}:\ForgeOptiApp" -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force "${L}:\ForgeOptiTest" | Out-Null
Copy-Item "$tmp\fo-vmtest\ForgeOpti.VmTest.exe" "${L}:\ForgeOptiTest\" -Force
foreach ($f in 'launch.ps1', 'suite2.ps1', 'portable.ps1', 'winget.ps1', 'ui.ps1', 'e2e.ps1', 'shots.ps1') { if (Test-Path "$here\$f") { Copy-Item "$here\$f" "${L}:\ForgeOptiTest\" -Force } }
Copy-Item "$tmp\fo-app" "${L}:\ForgeOptiApp" -Recurse -Force
Set-Content "${L}:\ForgeOptiTest\mode.txt" $Mode
if ($Filter) { Set-Content "${L}:\ForgeOptiTest\filter.txt" $Filter }
if (Test-Path "$tmp\rel\srv") { Copy-Item "$tmp\rel\srv" "${L}:\ForgeOptiTest\srv" -Recurse -Force }
if ($Mode -eq 'portable') { Copy-Item "$tmp\fo-portable\ForgeOpti-Portable.exe" "${L}:\ForgeOptiTest\" -Force }
if ($Mode -eq 'e2e') {
    # Les VRAIS livrables : installeur (e2e) ; le portable est testé par le mode 'portable'.
    $dist = Join-Path (Split-Path (Split-Path $here -Parent) -Parent) 'dist'
    Copy-Item (Get-ChildItem "$dist\ForgeOpti-Setup-*.exe" | Select-Object -First 1).FullName "${L}:\ForgeOptiTest\" -Force
}
reg load HKLM\VMSYS "${L}:\Windows\System32\config\SYSTEM" | Out-Null
$s = 'HKLM:\VMSYS\ControlSet001\Services'
foreach ($n in 'wuauserv', 'UsoSvc', 'WaaSMedicSvc') { if (Test-Path "$s\$n") { Set-ItemProperty "$s\$n" Start 4 -Type DWord } }
$k = "$s\ForgeOptiTest"; New-Item $k -Force | Out-Null
Set-ItemProperty $k Type 16 -Type DWord; Set-ItemProperty $k Start 2 -Type DWord; Set-ItemProperty $k ErrorControl 0 -Type DWord
Set-ItemProperty $k ImagePath 'cmd.exe /c start "" /b powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\ForgeOptiTest\launch.ps1' -Type ExpandString
Set-ItemProperty $k ObjectName LocalSystem
[gc]::Collect(); Start-Sleep 1
reg unload HKLM\VMSYS | Out-Null
Dismount-VHD -Path $p
Start-VM $Vm
"VM demarree ($Mode) : $((Get-VM $Vm).State), libre D: $([math]::Round((Get-PSDrive D).Free/1GB,1)) Go"

