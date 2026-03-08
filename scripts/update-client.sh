#!/usr/bin/env bash
# Build the Whispr client from the git repo.
# Usage:
#   ./scripts/update-client.sh              # Build for current platform
#   ./scripts/update-client.sh linux-x64     # Build for specific RID
#   ./scripts/update-client.sh --all         # Build for all platforms
#
# RIDs: win-x64, linux-x64, osx-x64, osx-arm64

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/Whispr.Client/Whispr.Client.csproj"
DIST_DIR="$REPO_ROOT/dist"

# Detect current platform RID
detect_rid() {
  case "$(uname -s)" in
    Linux)
      case "$(uname -m)" in
        x86_64) echo "linux-x64" ;;
        aarch64|arm64) echo "linux-arm64" ;;
        *) echo "linux-x64" ;;
      esac
      ;;
    Darwin)
      case "$(uname -m)" in
        arm64) echo "osx-arm64" ;;
        x86_64) echo "osx-x64" ;;
        *) echo "osx-arm64" ;;
      esac
      ;;
    MINGW*|MSYS*)
      echo "win-x64"
      ;;
    *)
      echo "linux-x64"
      ;;
  esac
}

BUILD_ALL=0
RID=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --all) BUILD_ALL=1; shift ;;
    -h|--help)
      echo "Usage: $0 [RID] [--all]"
      echo ""
      echo "  RID      Target runtime (e.g. linux-x64, win-x64, osx-arm64)"
      echo "  --all    Build for all platforms"
      echo ""
      echo "If no RID given, builds for current platform."
      exit 0
      ;;
    -*)
      echo "Unknown option: $1"; exit 1
      ;;
    *)
      RID="$1"; shift
      ;;
  esac
done

cd "$REPO_ROOT"

echo "Pulling latest changes..."
git pull

mkdir -p "$DIST_DIR"

if [[ $BUILD_ALL -eq 1 ]]; then
  for r in win-x64 linux-x64 osx-x64 osx-arm64; do
    echo ""
    echo "Building for $r..."
    ./scripts/publish-client.sh "$r"
  done
  echo ""
  echo "All builds complete. Output in $DIST_DIR/"
else
  RID="${RID:-$(detect_rid)}"
  echo "Building for $RID..."
  ./scripts/publish-client.sh "$RID"
  echo ""
  echo "Build complete: $DIST_DIR/whispr-client-$RID.*"
fi
