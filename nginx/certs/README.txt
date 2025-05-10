This folder contains local development TLS certificates.

❗ DO NOT commit any .crt, .key, or .pfx files to version control.

To generate certificates, use one of the provided scripts:

- generate-certs.ps1  (for PowerShell/Windows)
- generate-certs.sh   (for Bash/macOS/Linux)

Resulting files:
- localhost.crt → public certificate
- localhost.key → private key
