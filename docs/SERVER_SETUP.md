# Whispr Server Setup

This guide covers deploying the Whispr server for production: Docker, systemd, manual installation, firewall, and TLS certificates.

---

## Ports

| Port | Protocol | Purpose |
|------|----------|---------|
| **8443** | TCP/TLS | Control channel (login, rooms, key exchange) |
| **8444** | UDP | Audio relay |

Ensure both ports are open in your firewall and reachable by clients.

---

## TLS Certificate

The server requires a PFX certificate for the control channel. Set the path in `appsettings.json` (`CertificatePath`) and optionally the password via the `WHISPR_CERT_PASSWORD` environment variable.

### Let's Encrypt (recommended for public servers)

Let's Encrypt provides free, trusted certificates. Use Certbot or another ACME client.

**Prerequisites:** A domain pointing to your server (e.g. `voice.example.com`); ports 80 and 443 open (HTTP-01) or DNS access (DNS-01).

```bash
# Install Certbot (Ubuntu/Debian)
sudo apt install certbot

# Obtain a certificate (standalone mode – stops any service on port 80)
sudo certbot certonly --standalone -d voice.example.com
```

Certificates are stored at `/etc/letsencrypt/live/voice.example.com/`. Whispr needs a PFX file; convert with OpenSSL:

```bash
sudo openssl pkcs12 -export \
  -out /path/to/whispr/cert.pfx \
  -inkey /etc/letsencrypt/live/voice.example.com/privkey.pem \
  -in /etc/letsencrypt/live/voice.example.com/fullchain.pem \
  -passout pass:
```

Use an empty password for convenience, or set `WHISPR_CERT_PASSWORD` for a secure password.

**Auto-renewal:** Let's Encrypt certs expire in 90 days. Add to crontab:

```bash
0 0 * * * certbot renew --quiet --post-hook "systemctl restart whispr"
```

Restart the Whispr server after renewal so it loads the new certificate.

For custom CA (internal) or development self-signed certs, see the [Certificate Guide](CERTIFICATE_GUIDE.md).

---

## Docker

### Using a pre-built image

Images are published to GitHub Container Registry on each release. Replace `owner/whispr` with your repo.

```bash
docker run -d --name whispr \
  -p 8443:8443 -p 8444:8444/udp \
  -v /path/to/cert.pfx:/app/cert.pfx:ro \
  -v whispr-data:/app/data \
  -e WHISPR_CERT_PASSWORD= \
  ghcr.io/owner/whispr:v1.0.0
```

Persistent data (database, config) can be stored in a volume:

```bash
docker run -d --name whispr \
  -p 8443:8443 -p 8444:8444/udp \
  -v whispr-data:/app \
  -e CertificatePath=/app/cert.pfx \
  -e DatabasePath=/app/data/whispr.db \
  -e WHISPR_CERT_PASSWORD= \
  ghcr.io/owner/whispr:v1.0.0
```

Mount your `cert.pfx` into `/app` (e.g. `-v /host/certs/cert.pfx:/app/cert.pfx:ro`) and set `CertificatePath=cert.pfx` if the working directory is `/app`.

### Docker Compose

```yaml
services:
  whispr:
    image: ghcr.io/owner/whispr:latest
    ports:
      - "8443:8443"
      - "8444:8444/udp"
    volumes:
      - ./cert.pfx:/app/cert.pfx:ro
      - whispr-data:/app/data
    environment:
      - CertificatePath=/app/cert.pfx
      - DatabasePath=/app/data/whispr.db
      - WHISPR_CERT_PASSWORD=
    restart: unless-stopped

volumes:
  whispr-data:
```

### Build from source

From the repository root:

```bash
docker build -f docker/Dockerfile -t whispr-server .
docker run -d -p 8443:8443 -p 8444:8444/udp -v /path/to/cert.pfx:/app/cert.pfx:ro whispr-server
```

### Let's Encrypt (using certbot/certbot)

Uses the official [certbot/certbot](https://hub.docker.com/r/certbot/certbot) image. Requires port 80 for the ACME challenge.

**Prerequisites:** A domain pointing to your server (e.g. `voice.example.com`); ports 80, 8443, and 8444 open.

Create a `.env` file (or export the variables):

```
CERTBOT_DOMAIN=voice.example.com
CERTBOT_EMAIL=admin@example.com
```

Then from the repo root:

```bash
docker compose -f docker/docker-compose.letsencrypt.yml up -d
```

On first run, the certbot container obtains a certificate, converts it to PFX, and exits. Whispr then starts using the cert. To use a pre-built image instead of building from source, set `image: ghcr.io/owner/whispr:v1.0.0` in the compose file and remove the `build:` section.

**Renewal:** Let's Encrypt certs expire in 90 days. Add to crontab (ensure `.env` exists with `CERTBOT_DOMAIN` and `CERTBOT_EMAIL`):

```bash
0 0 * * * /path/to/whispr/docker/renew-certs.sh
```

Or run manually: `./docker/renew-certs.sh`

---

## systemd (Linux)

For a bare-metal or VM install, run the server as a systemd service.

1. **Install the server**  
   Extract the `whispr-server-linux-x64.tar.gz` release archive to e.g. `/opt/whispr/`.

2. **Place certificate**  
   Copy your `cert.pfx` to `/opt/whispr/cert.pfx` (or set `CertificatePath` in config).

3. **Create a service file** `/etc/systemd/system/whispr.service`:

```ini
[Unit]
Description=Whispr voice chat server
After=network.target

[Service]
Type=simple
WorkingDirectory=/opt/whispr
ExecStart=/opt/whispr/Whispr.Server
Restart=on-failure
RestartSec=5
Environment=WHISPR_CERT_PASSWORD=

[Install]
WantedBy=multi-user.target
```

4. **Enable and start**:

```bash
sudo systemctl daemon-reload
sudo systemctl enable whispr
sudo systemctl start whispr
sudo systemctl status whispr
```

5. **Firewall** (if using firewalld):

```bash
sudo firewall-cmd --permanent --add-port=8443/tcp
sudo firewall-cmd --permanent --add-port=8444/udp
sudo firewall-cmd --reload
```

---

## Manual installation

1. Download the server archive for your OS from [Releases](/releases).
2. Extract to a directory (e.g. `/opt/whispr` on Linux or `C:\Whispr` on Windows).
3. Copy or create `appsettings.json` (see `appsettings.json` in the project for defaults).
4. Place your TLS certificate as `cert.pfx` in the same directory (or set `CertificatePath`).
5. Set `WHISPR_CERT_PASSWORD` if the PFX is password-protected.
6. Run the executable:
   - **Linux/macOS:** `./Whispr.Server`
   - **Windows:** `Whispr.Server.exe`

---

## Firewall summary

| Port | Protocol | Action |
|------|----------|--------|
| 8443 | TCP | Allow inbound (control) |
| 8444 | UDP | Allow inbound (audio) |

Allow outbound for any HTTPS/DNS the server needs (e.g. certificate revocation checks).

---

## User management

The server persists users in a SQLite database. To add a user from the command line (when run from the project directory):

```bash
dotnet run --project src/Whispr.Server -- add-user myuser mypassword
```

When using the published binary:

```bash
./Whispr.Server add-user myuser mypassword
```

See the main [README](../README.md) for more on the server.
