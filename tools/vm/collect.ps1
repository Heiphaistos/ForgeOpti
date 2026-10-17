# Éteint proprement la VM, copie C:\ForgeOptiTest vers -Out (sans les .exe) et affiche les journaux.
param([string]$Out = "$env:TEMP\fo-run", [string]$Vm = 'W11 dev')
Stop-VM $Vm -Force
while ((Get-VM $Vm).State -ne 'Off') { Start-Sleep 2 }
$p = (Get-VM $Vm).HardDrives[0].Path
$d = Mount-VHD -Path $p -ReadOnly -Passthru | Get-Disk
$L = (Get-Partition -DiskNumber $d.Number | Get-Volume | Where-Object { $_.DriveLetter -and $_.Size -gt 50GB }).DriveLetter
Remove-Item $Out -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $Out -Force | Out-Null
Copy-Item "${L}:\ForgeOptiTest\*" $Out -Exclude *.exe -Recurse -ErrorAction SilentlyContinue
Dismount-VHD -Path $p
Get-ChildItem $Out | Select-Object Name
