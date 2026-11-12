<#
.SYNOPSIS
    Signs a Heimdall plugin package with your user certificate from the Windows certificate store
    (e.g. an AD CS auto-enrolled user certificate, smart card / non-exportable keys work).

.DESCRIPTION
    Writes the detached signature envelope (heimdall-plugin-signature/v1, JSON, no CMS): algorithm,
    your certificate chain (PEM, without the root), the signature over SHA-256 of the package file,
    signed-at. Upload package + envelope to POST /api/v1/plugin/signed; the backend verifies the chain
    and that the certificate maps to you (UPN / e-mail), then countersigns with the server certificate.

.EXAMPLE
    .\Sign-HeimdallPlugin.ps1 -Package .\spindle-monitor.json
    Uses the only valid certificate with a private key and Client Authentication / Code Signing EKU in Cert:\CurrentUser\My.

.EXAMPLE
    .\Sign-HeimdallPlugin.ps1 -Package .\spindle-monitor.json -Thumbprint 0123ABCD... -OutFile .\spindle-monitor.sig.json
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Package,
    [string]$Thumbprint,
    [string]$OutFile
)
$ErrorActionPreference = 'Stop'

$packagePath = (Resolve-Path -LiteralPath $Package).Path
$now = Get-Date
$candidates = @(Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.HasPrivateKey -and $_.NotAfter -gt $now -and $_.NotBefore -lt $now })

if ($Thumbprint) {
    $wanted = ($Thumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
    $cert = $candidates | Where-Object { $_.Thumbprint -eq $wanted } | Select-Object -First 1
    if (-not $cert) { throw "No valid certificate with a private key and thumbprint $wanted in Cert:\CurrentUser\My." }
}
else {
    $usable = @($candidates | Where-Object {
        $ekus = @($_.EnhancedKeyUsageList | ForEach-Object { $_.ObjectId })
        $ekus.Count -eq 0 -or $ekus -contains '1.3.6.1.5.5.7.3.3' -or $ekus -contains '1.3.6.1.5.5.7.3.2'
    })
    if ($usable.Count -ne 1) {
        $list = ($usable | ForEach-Object { "  $($_.Thumbprint)  $($_.Subject)  (until $($_.NotAfter))" }) -join [Environment]::NewLine
        throw "Choose the certificate with -Thumbprint. Candidates:$([Environment]::NewLine)$list"
    }
    $cert = $usable[0]
}

$bytes = [System.IO.File]::ReadAllBytes($packagePath)
$sha = [System.Security.Cryptography.SHA256]::Create()
try { $hash = $sha.ComputeHash($bytes) } finally { $sha.Dispose() }
$packageSha256 = -join ($hash | ForEach-Object { $_.ToString('x2') })

$rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
if ($rsa) {
    $signature = $rsa.SignHash($hash, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $algorithm = 'RSA-PKCS1-SHA256'
}
else {
    $ecdsa = [System.Security.Cryptography.X509Certificates.ECDsaCertificateExtensions]::GetECDsaPrivateKey($cert)
    if (-not $ecdsa) { throw 'The certificate key is neither RSA nor ECDSA.' }
    $signature = $ecdsa.SignHash($hash) # IEEE P1363 (r||s); Heimdall accepts it as well as DER
    $algorithm = 'ECDSA-SHA256'
}

# Chain from the Windows store (leaf first, roots left out: verifiers use their own trust store).
$chain = New-Object System.Security.Cryptography.X509Certificates.X509Chain
$chain.ChainPolicy.RevocationMode = [System.Security.Cryptography.X509Certificates.X509RevocationMode]::NoCheck
[void]$chain.Build($cert)
$pemBlocks = foreach ($element in $chain.ChainElements) {
    $c = $element.Certificate
    if ($c.Thumbprint -ne $cert.Thumbprint -and $c.Subject -eq $c.Issuer) { continue }
    $b64 = [Convert]::ToBase64String($c.RawData) -replace '(.{64})', "`$1`n"
    "-----BEGIN CERTIFICATE-----`n$($b64.TrimEnd("`n"))`n-----END CERTIFICATE-----"
}

$envelope = [ordered]@{
    format              = 'heimdall-plugin-signature/v1'
    algorithm           = $algorithm
    packageSha256       = $packageSha256
    signedAt            = $now.ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    certificateChainPem = ($pemBlocks -join "`n") + "`n"
    signature           = [Convert]::ToBase64String($signature)
}

if (-not $OutFile) { $OutFile = "$packagePath.sig.json" }
[System.IO.File]::WriteAllText($OutFile, ($envelope | ConvertTo-Json), (New-Object System.Text.UTF8Encoding($false)))
Write-Host "Signed $packagePath with $($cert.Subject) ($($cert.Thumbprint)) -> $OutFile"
