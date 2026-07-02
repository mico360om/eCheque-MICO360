#!/usr/bin/env bash
#
# Builds eCheque MICO360 into a macOS .app bundle and .dmg.
# RUN THIS ON A MAC (needs: dotnet 9 SDK, Xcode command-line tools for iconutil/hdiutil/codesign).
#
# Usage:
#   ./build-mac.sh                 # Apple-Silicon (osx-arm64), UNSIGNED
#   ./build-mac.sh osx-x64         # Intel build, UNSIGNED
#   MAC_SIGN_IDENTITY="Developer ID Application: Your Name (TEAMID)" ./build-mac.sh
#   # + notarize (after: xcrun notarytool store-credentials mico360 --apple-id .. --team-id .. --password ..):
#   MAC_SIGN_IDENTITY="Developer ID Application: ... (TEAMID)" MAC_NOTARY_PROFILE=mico360 ./build-mac.sh
#
set -euo pipefail

APP_NAME="eCheque MICO360"
BUNDLE_EXE="eCheque.MICO360.Mac"     # published executable name (matches Info.plist CFBundleExecutable)
RID="${1:-osx-arm64}"                # osx-arm64 | osx-x64
CONFIG="Release"
SIGN_ID="${MAC_SIGN_IDENTITY:-}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJ="$SCRIPT_DIR/eCheque.MICO360.Mac.csproj"
OUT="$SCRIPT_DIR/build/$RID"
APP="$OUT/$APP_NAME.app"

# Keep Info.plist version in sync with the .csproj <Version>.
VERSION="$(grep -oE '<Version>[^<]+' "$PROJ" | head -1 | sed 's/<Version>//')"
VERSION="${VERSION:-1.0.0}"

echo "==> eCheque MICO360  |  RID=$RID  |  version=$VERSION"
rm -rf "$OUT"; mkdir -p "$OUT"

echo "==> dotnet publish (self-contained)…"
dotnet publish "$PROJ" -c "$CONFIG" -r "$RID" --self-contained true \
    -p:PublishSingleFile=false -p:UseAppHost=true -o "$OUT/publish"

echo "==> Assembling .app bundle…"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
# Info.plist with the version injected
sed -e "s#<string>1.0.20</string>#<string>$VERSION</string>#g" "$SCRIPT_DIR/Info.plist" > "$APP/Contents/Info.plist"
cp -R "$OUT/publish/." "$APP/Contents/MacOS/"
chmod +x "$APP/Contents/MacOS/$BUNDLE_EXE" || true

echo "==> Building AppIcon.icns…"
iconutil -c icns "$SCRIPT_DIR/AppIcon.iconset" -o "$APP/Contents/Resources/AppIcon.icns"

if [ -n "$SIGN_ID" ]; then
    echo "==> Codesigning (hardened runtime) with: $SIGN_ID"
    # sign nested dylibs first, then the bundle
    find "$APP/Contents/MacOS" -type f \( -name "*.dylib" -o -name "*.so" \) -exec \
        codesign --force --timestamp --options runtime --sign "$SIGN_ID" {} \; || true
    codesign --force --deep --timestamp --options runtime \
        --entitlements "$SCRIPT_DIR/Entitlements.plist" --sign "$SIGN_ID" "$APP"
    codesign --verify --deep --strict --verbose=2 "$APP"
else
    echo "==> No MAC_SIGN_IDENTITY — UNSIGNED build (users: right-click the app > Open the first time)."
    codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || true  # ad-hoc so it launches locally
fi

echo "==> Creating .dmg…"
DMG="$SCRIPT_DIR/build/eCheque-MICO360-$VERSION-$RID.dmg"
rm -f "$DMG"
hdiutil create -volname "$APP_NAME" -srcfolder "$APP" -ov -format UDZO "$DMG" >/dev/null
echo "    $DMG"

if [ -n "${MAC_NOTARY_PROFILE:-}" ] && [ -n "$SIGN_ID" ]; then
    echo "==> Notarizing…"
    xcrun notarytool submit "$DMG" --keychain-profile "$MAC_NOTARY_PROFILE" --wait
    xcrun stapler staple "$APP"
    rm -f "$DMG"
    hdiutil create -volname "$APP_NAME" -srcfolder "$APP" -ov -format UDZO "$DMG" >/dev/null
    echo "==> Notarized & stapled."
fi

echo ""
echo "✅ Done."
echo "   App: $APP"
echo "   DMG: $DMG"
