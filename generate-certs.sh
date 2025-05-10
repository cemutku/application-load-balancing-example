#!/bin/bash

CERTS_DIR="nginx/certs"
CRT_FILE="$CERTS_DIR/localhost.crt"
KEY_FILE="$CERTS_DIR/localhost.key"

# Create certs directory if it doesn't exist
mkdir -p "$CERTS_DIR"

echo "🔐 Generating self-signed TLS certificate for localhost..."

openssl req -x509 -nodes -days 365 \
  -newkey rsa:2048 \
  -keyout "$KEY_FILE" \
  -out "$CRT_FILE" \
  -subj "/CN=localhost"

echo "✅ Certificate created:"
echo " - $CRT_FILE"
echo " - $KEY_FILE"
