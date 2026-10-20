param([Parameter(Mandatory)][string]$PrivateOut)
# Genere un couple ECDSA P-256. Affiche la cle PUBLIQUE (a coller dans UpdateVerifier.PublicKey) ;
# ecrit la cle PRIVEE (PKCS#8 base64) dans $PrivateOut : a mettre dans le secret CI FORGEOPTI_UPDATE_KEY, jamais dans le depot.
$k = [Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve]::NamedCurves.nistP256)
$p = $k.ExportParameters($false)
[Convert]::ToBase64String($k.ExportPkcs8PrivateKey()) | Set-Content -NoNewline -Encoding ascii $PrivateOut
'Cle publique : ' + [Convert]::ToBase64String([byte[]]($p.Q.X + $p.Q.Y))
