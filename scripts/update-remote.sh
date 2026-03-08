#!/usr/bin/env bash
# Update Whispr server from anywhere. For server admins who SSH into a live server.
#
# Usage:
#   curl -sSL https://raw.githubusercontent.com/OWNER/whispr/main/scripts/update-remote.sh | bash
#   # or with custom install path:
#   WHISPR_DIR=/opt/whispr curl -sSL ... | bash
#
# Finds the install at WHISPR_DIR (default /opt/whispr), pulls the latest image, and restarts.

set -e

WHISPR_DIR="${WHISPR_DIR:-/opt/whispr}"

if [[ ! -f "$WHISPR_DIR/docker-compose.yml" ]] || [[ ! -f "$WHISPR_DIR/.env" ]]; then
  echo "Whispr not found at $WHISPR_DIR (missing docker-compose.yml or .env)"
  echo "Set WHISPR_DIR if you installed elsewhere."
  exit 1
fi

cd "$WHISPR_DIR"
echo "Pulling latest Whispr image..."
docker compose -f docker-compose.yml --env-file .env pull
echo "Restarting Whispr..."
docker compose -f docker-compose.yml --env-file .env up -d
echo "Whispr updated."
