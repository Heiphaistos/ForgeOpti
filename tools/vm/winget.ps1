# Dans la VM : diagnostic des réinstallations (winget / Store) avec la sortie complète.
$o = 'C:\ForgeOptiTest\winget.log'
function L($m) { "[$(Get-Date -f HH:mm:ss)] $m" | Out-File $o -Append -Encoding utf8 }
function W($label, $argline) {
    L ">>> winget $argline"
    $psi = New-Object Diagnostics.ProcessStartInfo 'winget', $argline
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true; $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $psi.StandardOutputEncoding = [Text.Encoding]::UTF8
    $p = [Diagnostics.Process]::Start($psi)
    $out = $p.StandardOutput.ReadToEndAsync(); $err = $p.StandardError.ReadToEndAsync()
    if (-not $p.WaitForExit(420000)) { $p.Kill(); L "$label : DELAI DEPASSE" }
    L "$label exit=$($p.ExitCode)"
    L (($out.Result + $err.Result) -replace '[\x00-\x08\x0B-\x1F]', '' -replace '(\r?\n)\s*[-\\|/]\s*(\r?\n)', '$1')
}
Start-Service w32time -ErrorAction SilentlyContinue; w32tm /resync /force 2>&1 | Out-Null
L "winget $(winget --version) date=$(Get-Date)"
W 'sources' 'source list'
W 'maj-sources' 'source update'
W 'recherche-solitaire' 'search --id 9WZDNCRFHWD2 --source msstore --accept-source-agreements'
W 'install-solitaire' 'install --id 9WZDNCRFHWD2 --source msstore --accept-package-agreements --accept-source-agreements --silent'
W 'install-edge' 'install --id Microsoft.Edge --accept-package-agreements --accept-source-agreements --silent'
L ("appx solitaire: " + (Get-AppxPackage -AllUsers -Name Microsoft.MicrosoftSolitaireCollection | Measure-Object).Count)
New-Item C:\ForgeOptiTest\winget.done -Force | Out-Null
