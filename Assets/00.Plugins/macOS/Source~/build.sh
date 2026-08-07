#!/bin/sh
# Builds PollenGardenWindow.bundle next to this Source~ folder (universal binary).
# Rerun whenever PollenGardenWindow.m changes; commit the resulting bundle.
set -eu

SOURCE_DIR="$(cd "$(dirname "$0")" && pwd)"
BUNDLE_DIR="$SOURCE_DIR/../PollenGardenWindow.bundle"

mkdir -p "$BUNDLE_DIR/Contents/MacOS"

cat > "$BUNDLE_DIR/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>PollenGardenWindow</string>
    <key>CFBundleIdentifier</key>
    <string>com.confusedgamedev.pollengarden.window</string>
    <key>CFBundleName</key>
    <string>PollenGardenWindow</string>
    <key>CFBundlePackageType</key>
    <string>BNDL</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
</dict>
</plist>
PLIST

clang -fobjc-arc -bundle \
    -arch arm64 -arch x86_64 \
    -framework Cocoa -framework QuartzCore \
    -o "$BUNDLE_DIR/Contents/MacOS/PollenGardenWindow" \
    "$SOURCE_DIR/PollenGardenWindow.m"

echo "built: $BUNDLE_DIR"
lipo -info "$BUNDLE_DIR/Contents/MacOS/PollenGardenWindow"
