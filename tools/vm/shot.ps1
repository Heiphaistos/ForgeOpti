# Capture d'écran de la VM (800x600) ; réveille l'affichage invité avant (veille = écran noir).
param($Out = "$env:TEMP\vm.png", $Vm = 'W11 dev')
$id = (Get-VM $Vm).Id.ToString().ToUpper()
$kb = Get-CimInstance -Namespace root\virtualization\v2 -Query "SELECT * FROM Msvm_Keyboard WHERE SystemName='$id'"
Invoke-CimMethod -InputObject $kb -MethodName TypeKey -Arguments @{ keyCode = [uint32]16 } | Out-Null
Start-Sleep 3
$svc = Get-CimInstance -Namespace root\virtualization\v2 -ClassName Msvm_VirtualSystemManagementService
$cs = Get-CimInstance -Namespace root\virtualization\v2 -Query "SELECT * FROM Msvm_ComputerSystem WHERE Name='$id'"
$sd = Get-CimAssociatedInstance -InputObject $cs -ResultClassName Msvm_VirtualSystemSettingData | Where-Object { $_.VirtualSystemType -eq 'Microsoft:Hyper-V:System:Realized' } | Select-Object -First 1
$w = 800; $h = 600
$r = Invoke-CimMethod -InputObject $svc -MethodName GetVirtualSystemThumbnailImage -Arguments @{ TargetSystem = $sd; WidthPixels = [uint16]$w; HeightPixels = [uint16]$h }
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format16bppRgb565)
$bd = $bmp.LockBits((New-Object System.Drawing.Rectangle 0, 0, $w, $h), 'WriteOnly', $bmp.PixelFormat)
[Runtime.InteropServices.Marshal]::Copy($r.ImageData, 0, $bd.Scan0, $r.ImageData.Length); $bmp.UnlockBits($bd)
$bmp.Save($Out)
