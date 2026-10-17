# Dans la VM : diagnostic de la boite de dialogue de confirmation (quels elements UIA existent apres le clic sur « Optimiser en 1 clic »).
$o = 'C:\ForgeOptiTest\dlg.log'
function L($m) { "[$(Get-Date -f HH:mm:ss)] $m" | Out-File $o -Append -Encoding utf8 }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$A = [Windows.Automation.AutomationElement]; $PC = [Windows.Automation.PropertyCondition]
New-Item -ItemType Directory -Force C:\ProgramData\ForgeOpti | Out-Null
@{ theme = 'forge'; nav = 'side'; view = 'cards'; page = 'home'; autoUpdate = $false } | ConvertTo-Json | Set-Content C:\ProgramData\ForgeOpti\settings.json -Encoding utf8
Start-Sleep 120   # ecran de configuration Windows ferme depuis l'hote pendant l'attente
$app = Start-Process C:\ForgeOptiApp\ForgeOpti.exe -PassThru
$win = $null
for ($i = 0; $i -lt 60 -and -not $win; $i++) { Start-Sleep 1; $win = $A::RootElement.FindFirst('Children', (New-Object $PC ([Windows.Automation.AutomationElement]::ProcessIdProperty), $app.Id)) }
Start-Sleep 5
function Dump($tag) {
    $items = foreach ($e in $win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition)) {
        $t = $e.Current.ControlType.ProgrammaticName -replace 'ControlType\.', ''
        if ($t -in 'Button', 'Text', 'Window', 'Pane' -and $e.Current.Name) { "$t[$($e.Current.Name)]" }
    }
    L "$tag : $($items.Count) elements"
    L (($items | Select-Object -First 80) -join ' | ')
}
Dump 'avant clic'
foreach ($e in $win.FindAll('Descendants', [Windows.Automation.Condition]::TrueCondition)) {
    if ($e.Current.Name -like '*Optimiser en 1 clic*') { L "bouton trouve type=$($e.Current.ControlType.ProgrammaticName)"; try { $e.GetCurrentPattern([Windows.Automation.InvokePattern]::Pattern).Invoke(); L 'invoke ok' } catch { L "invoke KO $($_.Exception.Message)" }; break }
}
Start-Sleep 3
Dump 'apres clic'
$bmp = New-Object Drawing.Bitmap 1000, 700
$g = [Drawing.Graphics]::FromImage($bmp); $r = $win.Current.BoundingRectangle
$g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, (New-Object Drawing.Size ([int]$r.Width), ([int]$r.Height)))
$bmp.Save('C:\ForgeOptiTest\dlg-apres-clic.png')
New-Item C:\ForgeOptiTest\dlg.done -Force | Out-Null
