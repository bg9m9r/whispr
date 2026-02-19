# Certificate Guide for Whispr

This guide explains how to obtain and configure TLS certificates for Whispr servers. For **Let's Encrypt** (recommended for public servers), see the [Server Setup](SERVER_SETUP.md#lets-encrypt-recommended-for-public-servers) guide.

---

## Custom CA (Internal / Self-Hosted)

For internal networks or when you control all clients, use a private CA.

### Create a CA

```bash
# Create CA key and cert
openssl genrsa -out ca.key 4096
openssl req -new -x509 -days 3650 -key ca.key -out ca.crt \
  -subj "/CN=Whispr Internal CA"

# Create server key and CSR
openssl genrsa -out server.key 2048
openssl req -new -key server.key -out server.csr \
  -subj "/CN=voice.internal.example.com"
```

### Sign the Server Certificate

```bash
# Create extensions file for SAN (Subject Alternative Name)
echo "subjectAltName=DNS:voice.internal.example.com,DNS:localhost,IP:127.0.0.1" > ext.cnf

# Sign
openssl x509 -req -in server.csr -CA ca.crt -CAkey ca.key \
  -CAcreateserial -out server.crt -days 365 -extfile ext.cnf
```

### Create PFX

```bash
openssl pkcs12 -export -out cert.pfx \
  -inkey server.key -in server.crt -certfile ca.crt \
  -passout pass:
```

### Client Trust

For custom CA certs, clients must trust your CA. Options:

1. **Add CA to system store** – Install `ca.crt` in the OS trust store (e.g., `update-ca-trust` on Linux).
2. **Certificate pinning** – Add the server's cert hash to `~/.config/whispr/server-pins.json` (see [Certificate Pinning](#certificate-pinning-client) below).

---

## Development (Self-Signed)

For local development only:

```bash
# Generate self-signed cert (no CA)
dotnet dev-certs https -ep cert.pfx -p ""
```

Or with OpenSSL:

```bash
openssl req -x509 -newkey rsa:2048 -keyout key.pem -out cert.pem -days 365 -nodes \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

openssl pkcs12 -export -out cert.pfx -inkey key.pem -in cert.pem -passout pass:
```

**Note:** Self-signed certs are only accepted when connecting to `localhost` in DEBUG builds or when `WHISPR_ALLOW_DEV_CERT=1` is set. The client will show a security warning before connecting.

---

## Server Configuration

Set the certificate path in `appsettings.json` or via environment:

```json
{
  "CertificatePath": "cert.pfx",
  "ControlPort": 8443,
  "AudioPort": 8444
}
```

Certificate password: set `WHISPR_CERT_PASSWORD` environment variable (recommended for production).

---

## Certificate Pinning (Client)

For extra security, you can pin expected certificate hashes for known servers. When a pin is configured, the client only accepts connections if the server's certificate matches the pinned hash.

### Config File

Create or edit `~/.config/whispr/server-pins.json`:

```json
{
  "voice.example.com:8443": "base64sha256spki...",
  "192.168.1.100:8443": "base64sha256spki..."
}
```

Keys are `host:port`. Values are base64-encoded SHA256 hashes of the certificate's Subject Public Key Info (SPKI).

### Getting the Pin

**From a PEM certificate file:**

```bash
# Extract SPKI and compute SHA256 (base64)
openssl x509 -in fullchain.pem -pubkey -noout | \
  openssl pkey -pubin -outform der | \
  openssl dgst -sha256 -binary | \
  base64
```

**From the server (first connect with "Connect anyway" to get the cert):** Use a tool or script that fetches the cert and computes the hash. Add the hash manually to `server-pins.json`.
