#!/usr/bin/env bash
# Whispr server setup for Ubuntu (Docker)
# Run on a fresh Ubuntu server with Docker installed (e.g. DigitalOcean Docker droplet).
#
# Usage:
#   curl -sSL https://raw.githubusercontent.com/OWNER/whispr/main/scripts/setup-server.sh | bash
#   # or from a cloned repo:
#   ./scripts/setup-server.sh
#
# Options (env vars or flags):
#   WHISPR_DOMAIN    - Your domain (e.g. voice.example.com)
#   WHISPR_EMAIL     - Email for Let's Encrypt
#   WHISPR_IMAGE     - Docker image (e.g. ghcr.io/owner/whispr:latest)
#   WHISPR_DIR       - Install directory (default: /opt/whispr)
#   WHISPR_SKIP_FIREWALL - Set to 1 to skip UFW rules

set -e

# --- Parse args ---
while [[ $# -gt 0 ]]; do
  case "$1" in
    --domain)   WHISPR_DOMAIN="$2";   shift 2 ;;
    --email)    WHISPR_EMAIL="$2";    shift 2 ;;
    --image)    WHISPR_IMAGE="$2";    shift 2 ;;
    --dir)      WHISPR_DIR="$2";     shift 2 ;;
    --skip-firewall) WHISPR_SKIP_FIREWALL=1; shift ;;
    -h|--help)
      echo "Usage: $0 [--domain DOMAIN] [--email EMAIL] [--image IMAGE] [--dir DIR] [--skip-firewall]"
      echo ""
      echo "  --domain   Domain for TLS (e.g. voice.example.com)"
      echo "  --email    Email for Let's Encrypt"
      echo "  --image    Docker image (e.g. ghcr.io/owner/whispr:latest)"
      echo "  --dir      Install directory (default: /opt/whispr)"
      echo "  --skip-firewall  Skip UFW rules"
      exit 0
      ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

# --- Detect repo root (if running from clone) ---
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" 2>/dev/null && pwd || true)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." 2>/dev/null && pwd || true)"
if [[ -d "$REPO_ROOT/.git" ]]; then
  IN_REPO=1
  DEFAULT_IMAGE="ghcr.io/$(git -C "$REPO_ROOT" remote get-url origin 2>/dev/null | sed -E 's|.*[:/]([^/]+)/([^/.]+).*|\1/\2|' || echo 'owner/whispr'):latest"
else
  IN_REPO=0
  DEFAULT_IMAGE="ghcr.io/owner/whispr:latest"
fi

# --- Prompts ---
echo "=== Whispr Server Setup ==="
echo ""

WHISPR_DIR="${WHISPR_DIR:-/opt/whispr}"
mkdir -p "$WHISPR_DIR"
cd "$WHISPR_DIR"

if [[ -z "$WHISPR_DOMAIN" ]]; then
  read -rp "Domain (e.g. voice.example.com): " WHISPR_DOMAIN
  [[ -z "$WHISPR_DOMAIN" ]] && { echo "Domain required."; exit 1; }
fi

if [[ -z "$WHISPR_EMAIL" ]]; then
  read -rp "Email (for Let's Encrypt): " WHISPR_EMAIL
  [[ -z "$WHISPR_EMAIL" ]] && { echo "Email required."; exit 1; }
fi

if [[ -z "$WHISPR_IMAGE" ]]; then
  echo "Docker image (default: $DEFAULT_IMAGE)"
  read -rp "Image: " WHISPR_IMAGE
  WHISPR_IMAGE="${WHISPR_IMAGE:-$DEFAULT_IMAGE}"
fi

# --- Generate encryption key ---
if [[ -f "$WHISPR_DIR/.env" ]] && grep -q "WHISPR_MESSAGE_ENCRYPTION_KEY=" "$WHISPR_DIR/.env" 2>/dev/null; then
  WHISPR_MESSAGE_ENCRYPTION_KEY="$(grep "WHISPR_MESSAGE_ENCRYPTION_KEY=" "$WHISPR_DIR/.env" | cut -d= -f2-)"
  echo "Using existing WHISPR_MESSAGE_ENCRYPTION_KEY from .env"
else
  WHISPR_MESSAGE_ENCRYPTION_KEY="$(openssl rand -base64 32)"
  echo "Generated new message encryption key (saved to .env)"
fi

# --- Create .env ---
cat > "$WHISPR_DIR/.env" << EOF
CERTBOT_DOMAIN=$WHISPR_DOMAIN
CERTBOT_EMAIL=$WHISPR_EMAIL
WHISPR_MESSAGE_ENCRYPTION_KEY=$WHISPR_MESSAGE_ENCRYPTION_KEY
EOF

# --- Create docker-compose.yml ---
COMPOSE_FILE="$WHISPR_DIR/docker-compose.yml"
cat > "$COMPOSE_FILE" << 'COMPOSE_EOF'
services:
  certbot:
    image: certbot/certbot
    container_name: whispr-certbot
    volumes:
      - whispr-letsencrypt:/etc/letsencrypt
      - whispr-certs:/certs
    environment:
      - CERTBOT_DOMAIN=${CERTBOT_DOMAIN}
      - CERTBOT_EMAIL=${CERTBOT_EMAIL}
    entrypoint: /bin/sh -c
    command:
      - |
        if [ ! -f /etc/letsencrypt/live/$${CERTBOT_DOMAIN}/fullchain.pem ]; then
          certbot certonly --standalone -d $${CERTBOT_DOMAIN} --email $${CERTBOT_EMAIL} --agree-tos --non-interactive
        else
          certbot renew --quiet --standalone
        fi
        openssl pkcs12 -export -out /certs/cert.pfx \
          -inkey /etc/letsencrypt/live/$${CERTBOT_DOMAIN}/privkey.pem \
          -in /etc/letsencrypt/live/$${CERTBOT_DOMAIN}/fullchain.pem \
          -passout pass:
        echo "Certificate ready at /certs/cert.pfx"
    ports:
      - "80:80"
    restart: "no"

  whispr:
    image: WHISPR_IMAGE_PLACEHOLDER
    container_name: whispr
    ports:
      - "8443:8443"
      - "8444:8444/udp"
    volumes:
      - whispr-certs:/app/certs:ro
      - whispr-data:/app/data
    environment:
      - CertificatePath=/app/certs/cert.pfx
      - DatabasePath=/app/data/whispr.db
      - WHISPR_CERT_PASSWORD=
      - WHISPR_MESSAGE_ENCRYPTION_KEY=${WHISPR_MESSAGE_ENCRYPTION_KEY}
    restart: unless-stopped
    depends_on:
      certbot:
        condition: service_completed_successfully

volumes:
  whispr-letsencrypt:
  whispr-certs:
  whispr-data:
COMPOSE_EOF

sed -i "s|WHISPR_IMAGE_PLACEHOLDER|$WHISPR_IMAGE|g" "$COMPOSE_FILE"

# --- Check port 80 ---
if ss -tlnp 2>/dev/null | grep -q ':80 ' || netstat -tlnp 2>/dev/null | grep -q ':80 '; then
  echo "WARNING: Port 80 appears to be in use. Certbot needs it for Let's Encrypt."
  read -rp "Continue anyway? [y/N] " cont
  [[ "${cont,,}" != "y" && "${cont,,}" != "yes" ]] && exit 1
fi

# --- Firewall ---
if [[ "${WHISPR_SKIP_FIREWALL:-0}" != "1" ]] && command -v ufw &>/dev/null; then
  echo ""
  read -rp "Configure UFW to allow ports 80, 8443, 8444? [Y/n] " do_fw
  if [[ "${do_fw,,}" != "n" && "${do_fw,,}" != "no" ]]; then
    sudo ufw allow 80/tcp comment "Whispr certbot"
    sudo ufw allow 8443/tcp comment "Whispr control"
    sudo ufw allow 8444/udp comment "Whispr audio"
    if ! sudo ufw status | grep -q "Status: active"; then
      echo "UFW is inactive. Enable with: sudo ufw enable"
    else
      sudo ufw reload
    fi
    echo "Firewall rules added."
  fi
fi

# --- Pull and run ---
echo ""
echo "Pulling images and starting services..."
docker compose -f "$COMPOSE_FILE" --env-file "$WHISPR_DIR/.env" pull
docker compose -f "$COMPOSE_FILE" --env-file "$WHISPR_DIR/.env" up -d

echo ""
echo "Waiting for Whispr to start (certbot runs first, then Whispr)..."
for i in {1..60}; do
  if docker ps --format '{{.Names}}' | grep -q '^whispr$' && docker exec whispr true 2>/dev/null; then
    break
  fi
  sleep 2
done

if ! docker ps --format '{{.Names}}' | grep -q '^whispr$'; then
  echo "Whispr container may not have started. Check: docker compose -f $COMPOSE_FILE logs"
  exit 1
fi

# --- Create admin user ---
echo ""
echo "Create the first admin user:"
read -rp "Admin username: " admin_user
read -rsp "Admin password: " admin_pass
echo ""

if [[ -n "$admin_user" && -n "$admin_pass" ]]; then
  docker exec whispr ./Whispr.Server add-user "$admin_user" "$admin_pass" --admin
  echo "Admin user '$admin_user' created."
else
  echo "Skipped. Create an admin later with:"
  echo "  docker exec -it whispr ./Whispr.Server add-user USERNAME PASSWORD --admin"
fi

# --- Cron for renewal ---
RENEW_SCRIPT="$WHISPR_DIR/renew-certs.sh"
cat > "$RENEW_SCRIPT" << 'RENEW_EOF'
#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
docker compose -f docker-compose.yml --env-file .env run --rm certbot
docker compose -f docker-compose.yml restart whispr
RENEW_EOF
chmod +x "$RENEW_SCRIPT"

if command -v crontab &>/dev/null; then
  echo ""
  read -rp "Add cron job for certificate renewal (daily)? [Y/n] " do_cron
  if [[ "${do_cron,,}" != "n" && "${do_cron,,}" != "no" ]]; then
    (crontab -l 2>/dev/null | grep -v "renew-certs.sh" || true; echo "0 3 * * * $RENEW_SCRIPT") | crontab -
    echo "Cron job added for daily renewal at 3 AM."
  fi
fi

# --- Summary ---
echo ""
echo "=== Setup complete ==="
echo ""
echo "Whispr is running at:"
echo "  Control: https://$WHISPR_DOMAIN:8443"
echo "  Audio:   $WHISPR_DOMAIN:8444 (UDP)"
echo ""
echo "Connect with the client using: $WHISPR_DOMAIN : 8443"
echo ""
echo "Files:"
echo "  Directory: $WHISPR_DIR"
echo "  Compose:   $COMPOSE_FILE"
echo "  Env:       $WHISPR_DIR/.env"
echo ""
echo "Back up WHISPR_MESSAGE_ENCRYPTION_KEY from .env — you need it to restore the database."
echo ""
echo "Useful commands:"
echo "  docker compose -f $COMPOSE_FILE logs -f    # View logs"
echo "  docker exec -it whispr ./Whispr.Server add-user USER PASS --admin  # Add user"
echo "  $RENEW_SCRIPT    # Renew certificates manually"
echo ""
