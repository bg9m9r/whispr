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

**Prerequisites:** A domain pointing to your server (e.g. `voice.example.com`); port 80 open for HTTP-01 challenges (or DNS access for DNS-01).

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

**Auto-renewal:** Let's Encrypt certs expire in 90 days. Restart the server after renewal so it loads the new certificate.

- **systemd install:** Add to crontab: `0 0 * * * certbot renew --quiet --post-hook "systemctl restart whispr"`
- **Docker install:** Use the [Docker Let's Encrypt flow](#lets-encrypt-using-certbotcertbot) below; its renewal script converts the cert to PFX and restarts the container. Do not use the systemctl post-hook — there is no systemd service when running in Docker.

For custom CA (internal) or development self-signed certs, see the [Certificate Guide](CERTIFICATE_GUIDE.md).

---

## Message encryption (database)

When using a database (`DatabasePath` set), the server requires **WHISPR_MESSAGE_ENCRYPTION_KEY** for encrypting message content at rest. It must be a 32-byte value, base64-encoded.

**Generate a key (Linux/macOS):**

```bash
openssl rand -base64 32
```

Set it when running the server:

```bash
export WHISPR_MESSAGE_ENCRYPTION_KEY="your-base64-key-here"
./Whispr.Server
```

Or in Docker/systemd, pass `-e WHISPR_MESSAGE_ENCRYPTION_KEY=...` or `Environment=WHISPR_MESSAGE_ENCRYPTION_KEY=...`.

**Important:** Back up this key. If you lose it, existing encrypted messages cannot be decrypted. Rotating the key requires re-encrypting existing messages (not currently automated).

**Local testing only:** To run without a key (messages stored unencrypted), set `WHISPR_DEV_SKIP_MESSAGE_ENCRYPTION=1`. Do not use in production.

---

## Updating

### Server (Docker)

If you used the [setup script](#quick-setup-ubuntu), an `update.sh` script was created in your install directory (e.g. `/opt/whispr`). To pull the latest image and restart:

```bash
cd /opt/whispr   # or your WHISPR_DIR
./update.sh
```

Or manually:

```bash
docker compose -f docker-compose.yml --env-file .env pull
docker compose -f docker-compose.yml --env-file .env up -d
```

**From the git repo** (build and push a new image, then update the server):

```bash
./scripts/update-server.sh --push
# Then on the server: ./update.sh
```

### Client

**From the git repo** (build the client for your platform):

```bash
./scripts/update-client.sh              # Current platform
./scripts/update-client.sh linux-x64    # Specific platform
./scripts/update-client.sh --all        # All platforms
```

Output goes to `dist/`. End users can download new releases from [GitHub Releases](/releases).

---

## Updating

### Server (Docker)

If you used the [setup script](#quick-setup-ubuntu), an `update.sh` script was created in your install directory (e.g. `/opt/whispr`). To pull the latest image and restart:

```bash
cd /opt/whispr   # or your WHISPR_DIR
./update.sh
```

To build and push a new image from the git repo (for maintainers):

```bash
./scripts/update-server.sh --push
```

Then on your server, run `./update.sh` as above.

### Client (from source)

From a cloned repo, pull and build the client for your platform:

```bash
./scripts/update-client.sh              # Build for current platform
./scripts/update-client.sh linux-x64     # Build for specific RID
./scripts/update-client.sh --all         # Build for all platforms
```

Output goes to `dist/` (e.g. `dist/whispr-client-linux-x64.tar.gz`).

---

## Version compatibility

The server requires clients to be at least the same version as the server. On login, the server checks the client version; clients older than the server are rejected with a clear error (e.g. "Client version 0.9.0 is too old. Server requires 1.0.0 or newer. Please update your client."). Updated or newer clients can always connect.

---

## Docker

### Quick setup (Ubuntu)

On an Ubuntu server with Docker installed (e.g. DigitalOcean Docker droplet), run the setup script to configure Let's Encrypt, create the encryption key, and start Whispr:

```bash
curl -sSL https://raw.githubusercontent.com/OWNER/whispr/main/scripts/setup-server.sh | bash
```

Or from a cloned repo:

```bash
./scripts/setup-server.sh
```

The script prompts for domain, email, and Docker image, then sets up certificates, firewall (UFW), admin user, and a cron job for certificate renewal. Replace `OWNER/whispr` with your GitHub repo.

### Updating

**On the server** (Docker install via setup-server.sh): Run the update script created during setup:

```bash
/opt/whispr/update.sh
```

This pulls the latest image and restarts Whispr. If you installed to a different directory, run `./update.sh` from that directory.

**From the git repo** (build and push a new image):

```bash
./scripts/update-server.sh --push
```

This pulls latest code, builds the Docker image, and pushes to ghcr.io. Then on your server, run `./update.sh` to pull and restart.

**Client** (build from source):

```bash
./scripts/update-client.sh           # Build for current platform
./scripts/update-client.sh --all     # Build for all platforms
./scripts/update-client.sh linux-x64 # Build for specific platform
```

Output goes to `dist/`. End users can download releases from GitHub instead.

### Using a pre-built image

Images are published to GitHub Container Registry on each release. Replace `owner/whispr` with your repo.

```bash
docker run -d --name whispr \
  -p 8443:8443 -p 8444:8444/udp \
  -v /path/to/cert.pfx:/app/cert.pfx:ro \
  -v whispr-data:/app/data \
  -e WHISPR_CERT_PASSWORD= \
  -e WHISPR_MESSAGE_ENCRYPTION_KEY=your-base64-key-here \
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
  -e WHISPR_MESSAGE_ENCRYPTION_KEY=your-base64-key-here \
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
      - WHISPR_MESSAGE_ENCRYPTION_KEY=your-base64-key-here
    restart: unless-stopped

volumes:
  whispr-data:
```

### Build from source

From the repository root:

```bash
docker build -f docker/Dockerfile -t whispr-server .
docker run -d --name whispr \
  -p 8443:8443 -p 8444:8444/udp \
  -v /path/to/cert.pfx:/app/cert.pfx:ro \
  -v whispr-data:/app/data \
  -e CertificatePath=/app/cert.pfx \
  -e DatabasePath=/app/data/whispr.db \
  -e WHISPR_CERT_PASSWORD= \
  -e WHISPR_MESSAGE_ENCRYPTION_KEY=your-base64-key-here \
  whispr-server
```

Mount your cert and set paths as needed; see the pre-built examples above for volume layout.

### Let's Encrypt (using certbot/certbot)

Uses the official [certbot/certbot](https://hub.docker.com/r/certbot/certbot) image. Requires port 80 for the ACME challenge.

**Prerequisites:** A domain pointing to your server (e.g. `voice.example.com`); ports 80 (certbot), 8443, and 8444 open.

Create a `.env` file (or export the variables):

```
CERTBOT_DOMAIN=voice.example.com
CERTBOT_EMAIL=admin@example.com
```

Then from the repo root:

```bash
docker compose -f docker/docker-compose.letsencrypt.yml up -d
```

On first run, the certbot container obtains a certificate, converts it to PFX, and exits. Whispr then starts using the cert. Add `WHISPR_MESSAGE_ENCRYPTION_KEY` to the whispr service `environment` block (see [Message encryption](#message-encryption-database)). To use a pre-built image instead of building from source, set `image: ghcr.io/owner/whispr:v1.0.0` in the compose file and remove the `build:` section.

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
Environment=WHISPR_MESSAGE_ENCRYPTION_KEY=your-base64-key-here

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
| 80 | TCP | Allow inbound if using certbot standalone for Let's Encrypt auto-renewal |

Allow outbound for any HTTPS/DNS the server needs (e.g. certificate revocation checks).

---

## User management

The server persists users in a SQLite database. **Production deployments must create an admin user** before first use; the server does not seed default credentials in production.

To add a user from the command line (when run from the project directory):

```bash
dotnet run --project src/Whispr.Server -- add-user myuser mypassword
```

To add an **admin** user (required for production):

```bash
dotnet run --project src/Whispr.Server -- add-user admin mypassword --admin
```

When using the published binary:

```bash
./Whispr.Server add-user myuser mypassword
./Whispr.Server add-user admin mypassword --admin
```

When using Docker:

```bash
docker exec -it whispr ./Whispr.Server add-user myuser mypassword
docker exec -it whispr ./Whispr.Server add-user admin mypassword --admin
```

For development only, you can enable `SeedTestUsers: true` in `appsettings.json` to auto-seed `admin/admin` and `bob/bob` when the user store is empty. **Do not use in production.**

---

## Configuration

| Option | Default | Description |
|--------|---------|-------------|
| `ControlPort` | 8443 | TCP port for control channel (TLS) |
| `AudioPort` | 8444 | UDP port for audio relay |
| `CertificatePath` | cert.pfx | Path to PFX certificate |
| `DatabasePath` | whispr.db | SQLite database path |
| `SeedTestUsers` | false | Seed test users when empty (dev only) |
| `TokenLifetimeHours` | 24 | Session token expiry in hours |

The server allows one concurrent session per user. A second login from the same user is rejected with "Already logged in from another session" until the first session disconnects.

See the main [README](../README.md) for more on the server.
