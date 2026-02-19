#!/bin/bash
# Renew Let's Encrypt certs and restart Whispr.
# Add to crontab: 0 0 * * * /path/to/whispr/docker/renew-certs.sh
# Ensure CERTBOT_DOMAIN and CERTBOT_EMAIL are in .env or exported.

set -e
cd "$(dirname "$0")/.."

docker compose -f docker/docker-compose.letsencrypt.yml run --rm certbot
docker compose -f docker/docker-compose.letsencrypt.yml restart whispr
