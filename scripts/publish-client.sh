#!/usr/bin/env bash
# Publish Whispr client for a given runtime identifier.
# Usage: ./publish-client.sh <rid>
# RIDs: win-x64, linux-x64, osx-x64, osx-arm64

set -e

RID="${1:?Usage: $0 <rid> (e.g. win-x64, linux-x64, osx-x64, osx-arm64)}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT="$REPO_ROOT/src/Whispr.Client/Whispr.Client.csproj"
PUBLISH_DIR="$REPO_ROOT/src/Whispr.Client/bin/Release/net10.0/$RID/publish"
DIST_DIR="$REPO_ROOT/dist"
ARCHIVE_NAME="whispr-client-$RID"

cd "$REPO_ROOT"

echo "Publishing Whispr.Client for $RID..."
dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained -o "$PUBLISH_DIR"

mkdir -p "$DIST_DIR"

case "$RID" in
  win-*)
    (cd "$PUBLISH_DIR" && zip -r "$DIST_DIR/$ARCHIVE_NAME.zip" .)
    echo "Created $DIST_DIR/$ARCHIVE_NAME.zip"
    ;;
  *)
    (cd "$PUBLISH_DIR" && tar czf "$DIST_DIR/$ARCHIVE_NAME.tar.gz" .)
    echo "Created $DIST_DIR/$ARCHIVE_NAME.tar.gz"
    ;;
esac
