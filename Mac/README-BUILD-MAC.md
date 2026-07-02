# Building eCheque MICO360 for macOS

The macOS app is an [Avalonia](https://avaloniaui.net/) UI over the shared cross-platform
`Core` library (same business logic as the Windows app). It **must be built on a Mac** —
`.app` bundling, icon (`iconutil`), `.dmg` (`hdiutil`), and code-signing/notarization all
require macOS tooling.

## Prerequisites (on the Mac)
- **.NET 9 SDK** — https://dotnet.microsoft.com/download
- **Xcode command-line tools** — `xcode-select --install`
- (Signing, optional) an **Apple Developer account** + a *Developer ID Application* certificate

## Build (unsigned — quickest)
```bash
cd eCheque.MICO360/Mac
chmod +x build-mac.sh
./build-mac.sh            # Apple Silicon (osx-arm64)
./build-mac.sh osx-x64    # Intel
```
Output: `Mac/build/<rid>/eCheque MICO360.app` and `Mac/build/eCheque-MICO360-<ver>-<rid>.dmg`.

> Unsigned apps are blocked by Gatekeeper on first launch. Open with **right-click → Open →
> Open**, or `xattr -dr com.apple.quarantine "eCheque MICO360.app"`.

## Build (signed + notarized)
1. Create a notary profile once:
   ```bash
   xcrun notarytool store-credentials mico360 \
     --apple-id "you@example.com" --team-id "TEAMID" --password "app-specific-password"
   ```
2. Build:
   ```bash
   MAC_SIGN_IDENTITY="Developer ID Application: Your Name (TEAMID)" \
   MAC_NOTARY_PROFILE=mico360 \
   ./build-mac.sh
   ```
This code-signs with the hardened runtime + `Entitlements.plist`, notarizes, and staples.

## Files
| File | Purpose |
|------|---------|
| `build-mac.sh` | Publish → bundle `.app` → icon → sign → `.dmg` → notarize |
| `Info.plist` | Bundle metadata (id `com.mico360.echeque`, version auto-synced from `.csproj`) |
| `Entitlements.plist` | Hardened-runtime entitlements required by .NET/CoreCLR |
| `AppIcon.iconset/` | App icon PNGs (converted to `AppIcon.icns` at build time) |

## Notes
- Keep the app version in `eCheque.MICO360.Mac.csproj` `<Version>` — the script injects it into `Info.plist`.
- The `Core` project is shared with Windows; UI parity is tracked in the repo. Printing on macOS
  is routed through PDF generation + the native print/Preview path (Windows uses WPF printing).
