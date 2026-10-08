#!/bin/bash
set -euo pipefail

# Compile and package only. This script never opens the app or reads rekordbox settings.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$ROOT/dist/Rekordbox Setup Cloner.app"
BUILD="$ROOT/.build/app"
ICON_STYLE="${APP_ICON_STYLE:-sync}"
mkdir -p "$BUILD" "$APP/Contents/MacOS" "$APP/Contents/Resources"

# Render artwork only; the generator never starts the application.
xcrun swiftc -O -module-cache-path "$BUILD/module-cache" \
    "$ROOT/scripts/render-icon.swift" -o "$BUILD/render-icon"
"$BUILD/render-icon" "$ROOT/Resources" "$BUILD" "$ICON_STYLE"
for STYLE in record sync; do
    xcrun iconutil -c icns "$BUILD/AppIcon-$STYLE.iconset" -o "$ROOT/Resources/AppIcon-$STYLE.icns"
done
cp "$ROOT/Resources/AppIcon-$ICON_STYLE.icns" "$ROOT/Resources/AppIcon.icns"

for ARCH in arm64 x86_64; do
    xcrun swiftc -O -parse-as-library -swift-version 5 \
        -target "$ARCH-apple-macosx13.0" \
        -module-cache-path "$BUILD/module-cache" \
        "$ROOT"/Sources/RekordboxSetupCloner/*.swift \
        -o "$BUILD/RekordboxSetupCloner-$ARCH"
done

xcrun lipo -create "$BUILD/RekordboxSetupCloner-arm64" "$BUILD/RekordboxSetupCloner-x86_64" \
    -output "$APP/Contents/MacOS/RekordboxSetupCloner"
cp "$ROOT/Resources/Info.plist" "$APP/Contents/Info.plist"
cp "$ROOT/Resources/AppIcon.icns" "$APP/Contents/Resources/AppIcon.icns"
codesign --force --sign - "$APP"
ditto -c -k --keepParent "$APP" "$ROOT/dist/Rekordbox Setup Cloner.zip"
echo "Built: $APP"
