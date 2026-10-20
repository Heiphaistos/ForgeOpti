param([Parameter(Mandatory)][string]$Version, [Parameter(Mandatory)][string]$Repo)
# Produit dist\latest.json + latest.json.sig (ECDSA P-256 / SHA-256, signature IEEE P1363 en base64).
# La cle privee PKCS#8 base64 vient de la variable d'environnement FORGEOPTI_UPDATE_KEY (secret CI) ; echec si absente.
$ErrorActionPreference = 'Stop'
if (-not $env:FORGEOPTI_UPDATE_KEY) { throw 'Secret FORGEOPTI_UPDATE_KEY absent : release non signee refusee.' }
$zip = Join-Path $PSScriptRoot '..\dist\ForgeOpti-win-x64.zip' | Resolve-Path
$json = [ordered]@{
    version = $Version
    url     = "https://github.com/$Repo/releases/download/v$Version/ForgeOpti-win-x64.zip"
    size    = (Get-Item $zip).Length
    sha256  = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
} | ConvertTo-Json -Compress
$bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
$k = [Security.Cryptography.ECDsa]::Create()
$k.ImportPkcs8PrivateKey([Convert]::FromBase64String($env:FORGEOPTI_UPDATE_KEY), [ref]$null)
$sig = [Convert]::ToBase64String($k.SignData($bytes, [Security.Cryptography.HashAlgorithmName]::SHA256))
$dist = Split-Path $zip
[IO.File]::WriteAllBytes("$dist\latest.json", $bytes)
Set-Content -NoNewline -Encoding ascii "$dist\latest.json.sig" $sig
