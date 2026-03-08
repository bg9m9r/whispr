#!/usr/bin/env bash
# Build and optionally push the Whispr server Docker image from the git repo.
# Usage:
#   ./scripts/update-server.sh              # Build only (for local testing)
#   ./scripts/update-server.sh --push       # Build and push to ghcr.io
#
# After pushing, on your server run: ./update.sh (or docker compose pull && docker compose up -d)

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Detect image from git remote
if [[ -d "$REPO_ROOT/.git" ]]; then
  IMAGE="ghcr.io/$(git -C "$REPO_ROOT" remote get-url origin 2>/dev/null | sed -E 's|.*[:/]([^/]+)/([^/.]+).*|\1/\2|' || echo 'owner/whispr'):latest"
else
  IMAGE="ghcr.io/owner/whispr:latest"
fi

PUSH=0
while [[ $# -gt 0 ]]; do
  case "$1" in
    --push) PUSH=1; shift ;;
    -h|--help)
      echo "Usage: $0 [--push]"
      echo ""
      echo "  --push   Push image to registry after building (default: build only)"
      exit 0
      ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

cd "$REPO_ROOT"

echo "Pulling latest changes..."
git pull

echo "Building Docker image: $IMAGE"
docker build -f docker/Dockerfile -t "$IMAGE" .

if [[ $PUSH -eq 1 ]]; then
  echo "Pushing to registry..."
  docker push "$IMAGE"
  echo ""
  echo "Image pushed. On your server, run: ./update.sh"
  echo "  (or: docker compose -f docker-compose.yml --env-file .env pull && docker compose -f docker-compose.yml --env-file .env up -d)"
else
  echo ""
  echo "Image built locally. To push: $0 --push"
fi
