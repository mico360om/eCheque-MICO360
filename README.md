# eCheque MICO360

A secure Windows desktop application for preparing, printing, and tracking cheques against configurable bank layouts.

**Developer:** MICO360 Softwares
**Platform:** Windows (.NET 9, WPF)

## Features

- Multi-company support (isolated database per company)
- Configurable cheque profiles with a visual layout designer
- Amount-in-words conversion for OMR / Baisa (configurable wording, case, "Only" suffix)
- Cheque history, print history, and reprint tracking with mandatory reprint reasons
- Role-based access control (Admin / Accountant / Viewer) with enforced authorization
- Audit logging of all sensitive actions
- Backup & restore, PDF export, Excel export
- Terms & Conditions, Privacy Policy, and About pages (editable by admins)
- **Auto-update** from GitHub Releases with checksum verification and rollback

## Building

```bash
dotnet build
dotnet run
```

Requires the .NET 9 SDK (Windows).

## Auto-update / Releases

The app checks this repository's **latest GitHub Release** on startup and via
**Help & Legal → Check for Updates**.

To publish an update:

1. Bump `<Version>` in `eCheque.MICO360.csproj`.
2. Build and publish, then zip the published output (the `.zip` must contain the app files at its root).
3. Create a GitHub Release whose **tag** is the new version (e.g. `v1.1.0`).
4. Attach the `.zip` as a release asset.
5. (Recommended) Attach a `<asset>.sha256` file, or add a `SHA256: <hash>` line to the release notes, so the app can verify the download.
6. To force the update, include `[mandatory]` in the release notes.

The updater downloads the package, verifies the SHA-256, backs up the current
install, extracts the new version on restart, and rolls back automatically if
extraction fails. User data (database, settings, PDFs) lives outside the app
folder and is never modified by an update.

## License

© 2026 MICO360 Softwares. All rights reserved.
