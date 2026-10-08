#!/bin/bash
set -euo pipefail

# Build and package release assets without launching or testing the app.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$ROOT/Resources/Info.plist")"
if [[ ! "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo 'Expected a three-part app version in Resources/Info.plist.' >&2
    exit 1
fi

bash "$ROOT/scripts/build-app.sh"
OUT="$ROOT/dist/release"
ARCHIVE="Rekordbox-Setup-Cloner-v$VERSION-macos-universal.zip"
mkdir -p "$OUT"
cp "$ROOT/dist/Rekordbox Setup Cloner.zip" "$OUT/$ARCHIVE"
cd "$OUT"
shasum -a 256 "$ARCHIVE" > SHA256SUMS.txt
echo "Release assets: $OUT"
