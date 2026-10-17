# Exécuté DANS la VM par un service SYSTEM injecté hors-ligne : lance le test dans la session de l'utilisateur connecté, élevé, sans invite UAC.
$log = 'C:\ForgeOptiTest\launch.log'
New-Item -ItemType Directory -Force C:\ForgeOptiTest | Out-Null
"[$(Get-Date -f HH:mm:ss)] launch start" | Out-File $log -Append
Start-Sleep 20
for ($i = 0; $i -lt 180; $i++) {
    $u = (Get-CimInstance Win32_ComputerSystem).UserName
    if ($u -and (Get-Process explorer -ErrorAction SilentlyContinue)) { break }
    Start-Sleep 5
}
"user=$u" | Out-File $log -Append
function RunAs($name, $cmd) {
    schtasks /Create /TN $name /TR $cmd /SC ONCE /ST 00:00 /RU $u /RL HIGHEST /IT /F 2>&1 | Out-File $log -Append
    schtasks /Run /TN $name 2>&1 | Out-File $log -Append
}
$mode = if (Test-Path C:\ForgeOptiTest\mode.txt) { (Get-Content C:\ForgeOptiTest\mode.txt -Raw).Trim() } else { 'phases' }
if ($mode -eq 'suite2') {
    if (-not (Test-Path C:\ForgeOptiTest\suite2.started)) {
        New-Item C:\ForgeOptiTest\suite2.started -Force | Out-Null
        RunAs FOSuite2 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\ForgeOptiTest\suite2.ps1'
    }
}
elseif ($mode -eq 'portable') {
    if (-not (Test-Path C:\ForgeOptiTest\portable.started)) {
        New-Item C:\ForgeOptiTest\portable.started -Force | Out-Null
        RunAs FOPortable 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File C:\ForgeOptiTest\portable.ps1'
    }
}
else {
    if (-not (Test-Path C:\ForgeOptiTest\smoke.done)) {
        RunAs FOSmoke 'C:\ForgeOptiApp\ForgeOpti.exe --smoke'
        Start-Sleep 70
        New-Item C:\ForgeOptiTest\smoke.done -Force | Out-Null
    }
    RunAs FOTest 'C:\ForgeOptiTest\ForgeOpti.VmTest.exe C:\ForgeOptiTest'
}
"[$(Get-Date -f HH:mm:ss)] launch done ($mode)" | Out-File $log -Append
