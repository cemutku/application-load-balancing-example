# Paths
$certsDir = "nginx\certs"
$crtPath = "$certsDir\localhost.crt"
$keyPath = "$certsDir\localhost.key"
$pfxPath = "$certsDir\localhost.pfx"
$subject = "CN=localhost"
$pfxPassword = ConvertTo-SecureString -String "devcert123" -Force -AsPlainText

# Create certs directory if missing
if (-not (Test-Path $certsDir)) {
    New-Item -ItemType Directory -Path $certsDir | Out-Null
}

# Generate self-signed cert in user's personal store
Write-Host "Generating self-signed certificate..."
$cert = New-SelfSignedCertificate -DnsName "localhost" `
    -CertStoreLocation "cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(1)

# Export as .pfx
Write-Host "Exporting .pfx..."
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword

# Extract .crt and .key using OpenSSL
Write-Host "Extracting .crt and .key using OpenSSL..."
openssl pkcs12 -in $pfxPath -clcerts -nokeys -out $crtPath -passin pass:devcert123
openssl pkcs12 -in $pfxPath -nocerts -nodes -out $keyPath -passin pass:devcert123

Write-Host "✅ Certificate files created:"
Write-Host "`t$crtPath"
Write-Host "`t$keyPath"
