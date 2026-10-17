param([Parameter(Mandatory)][string]$Version)
# Produit dist\ : Setup.exe (installeur), Portable.exe (un seul .exe sans assistant), zip (mise à jour auto) + empreintes SHA-256.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
Remove-Item out, dist, obj-payload -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory dist, obj-payload | Out-Null

dotnet publish src/ForgeOpti.App -c Release -r win-x64 "-p:Version=$Version" -o out
if ($LASTEXITCODE) { throw 'publish app' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = Join-Path $root 'dist\ForgeOpti-win-x64.zip'
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'out'), $zip, [IO.Compression.CompressionLevel]::Optimal, $false)

# Version portable : charge utile Brotli embarquée dans un lanceur autonome.
# Zip « stocké » (sans compression) puis Brotli : bien plus petit que Brotli d'un zip déjà compressé.
$stored = Join-Path $root 'obj-payload\payload.zip'
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $root 'out'), $stored, [IO.Compression.CompressionLevel]::NoCompression, $false)
$payload = Join-Path $root 'obj-payload\payload.br'
dotnet run --project tools/Pack -c Release -- $stored $payload
if ($LASTEXITCODE) { throw 'pack' }
dotnet publish src/ForgeOpti.Portable -c Release "-p:Version=$Version" "-p:Payload=$payload" -o obj-payload/portable
if ($LASTEXITCODE) { throw 'publish portable' }
Copy-Item obj-payload/portable/ForgeOpti-Portable.exe "dist/ForgeOpti-Portable-$Version.exe"

# Installeur.
$iscc = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { $iscc = (Get-Command iscc -ErrorAction Stop).Source }
& $iscc /Q "/DAppVersion=$Version" "/DSourceDir=$root\out" installer\ForgeOpti.iss
if ($LASTEXITCODE) { throw 'iscc' }

Get-ChildItem dist -File | Where-Object Extension -ne '.sha256' | ForEach-Object {
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLower())  $($_.Name)" | Out-File -Encoding ascii "$($_.FullName).sha256"
}
Get-ChildItem dist | Select-Object Name, @{n = 'Mo'; e = { [math]::Round($_.Length / 1MB, 1) } }
