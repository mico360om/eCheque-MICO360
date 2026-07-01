# eCheque MICO360 — macOS Setup Guide

**Product:** eCheque MICO360
**Vendor:** MICO360 Softwares
**Platform:** macOS (Avalonia UI · .NET 9)
**Audience:** Administrators installing the app on a Mac

---

## 1. Overview

eCheque MICO360 is a secure desktop application for preparing, printing, and tracking cheques against configurable bank layouts. It is designed for use in Oman: the currency is the Omani Rial (**OMR**), where **1000 Baisa = 1 Rial**, and amounts use **3 decimal places**.

The macOS edition is a native **Avalonia UI (.NET 9)** application that shares a cross-platform **Core** library with the Windows (WPF) edition. All business features are identical across platforms.

---

## 2. Prerequisites

| Requirement | Details |
|-------------|---------|
| macOS version | macOS 12 (Monterey) or later |
| .NET SDK | .NET 9 SDK |
| Homebrew | Recommended for installing the .NET SDK |
| Git | Required only for the install-from-source method |
| Disk space | ~500 MB (SDK + app + data) |
| Printer | Any macOS-configured printer for cheque printing |

### Install Homebrew (if not already present)

```bash
/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
```

### Install the .NET 9 SDK

```bash
brew install --cask dotnet-sdk
```

Verify the installation:

```bash
dotnet --version
```

You should see a version starting with `9.`.

---

## 3. Install from Source

Use this method for development machines or when a packaged build is not available.

```bash
# 1. Clone the repository
git clone https://github.com/mico360om/eCheque-MICO360.git
cd eCheque-MICO360

# 2. Run the macOS project
dotnet run --project eCheque.MICO360.Mac
```

The first build will restore NuGet packages and may take a few minutes. On subsequent runs, the app launches directly.

---

## 4. Install from .dmg / .pkg (Packaged Build)

Use this method for end-user machines.

1. Download the latest `eCheque.MICO360.dmg` (or `.pkg`) from the [GitHub Releases page](https://github.com/mico360om/eCheque-MICO360).
2. **.dmg:** Double-click to mount, then drag **eCheque MICO360.app** into the **Applications** folder.
3. **.pkg:** Double-click and follow the installer prompts.
4. Launch the app from **Applications** or Launchpad.

### Unsigned / Un-notarized Builds (Gatekeeper)

If the build is not yet code-signed and notarized, macOS Gatekeeper will block it with a "cannot be opened because the developer cannot be verified" or "damaged" message. Clear the quarantine attribute:

```bash
xattr -dr com.apple.quarantine "/Applications/eCheque MICO360.app"
```

Alternatively, right-click the app → **Open** → **Open** on the confirmation dialog (first launch only).

> **Note on notarization:** Production releases are intended to be Apple Developer ID code-signed and notarized so that no `xattr` workaround is needed. Until notarization is in place, use the command above on the trusted download.

---

## 5. First Run

1. **Launch** the application.
2. **Create a company.** On first run, no companies exist. Use the **Companies** screen (or the first-run prompt) to create your first company. Each company gets its own encrypted database.
3. **Log in with the default administrator.** A default Admin account is provisioned for the new company.
   - Default username: `admin`
   - Default password: as documented in the release notes (change immediately).
4. **Change the default password.** Open **Users** (or your profile), edit the admin account, and set a strong password. Passwords are stored using **BCrypt** hashing.
5. Configure company details, currency, and paths in **Settings** (see the User Manual).

---

## 6. Data Locations

All application data on macOS is stored under:

```
~/Library/Application Support/eCheque_MICO360/
```

| Item | Location |
|------|----------|
| Databases | `~/Library/Application Support/eCheque_MICO360/` (one SQLite database per company) |
| Encryption | Databases are encrypted at rest via **SQLCipher** |
| PDF output | Path configured in **Settings → PDF path** |
| Backups | Path configured in **Settings → Backup path** |

To open the folder in Finder:

```bash
open ~/Library/Application\ Support/eCheque_MICO360/
```

---

## 7. Backup & Restore

Backup and restore are managed from **Settings**.

**Backup**
1. Open **Settings**.
2. Confirm the **Backup path**.
3. Click **Backup Now**. An encrypted copy of the company database is written to the backup path.

**Restore**
1. Open **Settings**.
2. Click **Restore** and select a previously created backup file.
3. Confirm. The current database is replaced with the backup.

> **Recommendation:** Keep backups on a separate volume or cloud-synced folder, and test a restore periodically.

---

## 8. Updating

The app checks for updates via **Help & Legal → Check for Updates**, pulling from **GitHub Releases**:

```
https://github.com/mico360om/eCheque-MICO360
```

- **Packaged build:** download the newer `.dmg`/`.pkg` and reinstall over the existing app. Your data under `~/Library/Application Support/eCheque_MICO360/` is preserved.
- **Source build:** `git pull` then `dotnet run --project eCheque.MICO360.Mac`.

---

## 9. Troubleshooting

| Symptom | Likely Cause | Resolution |
|---------|--------------|------------|
| "App cannot be opened, developer cannot be verified" | Unsigned/un-notarized build blocked by Gatekeeper | Run `xattr -dr com.apple.quarantine "/Applications/eCheque MICO360.app"` or right-click → Open |
| "App is damaged and can't be opened" | Quarantine attribute on downloaded build | Same as above |
| `dotnet: command not found` | .NET SDK not installed or not on PATH | `brew install --cask dotnet-sdk`; restart terminal |
| `dotnet --version` shows < 9 | Wrong SDK version | Install the .NET 9 SDK |
| Build fails restoring packages | No network / NuGet unreachable | Check internet connection and retry `dotnet run` |
| App launches but no companies listed | First run, no company created | Create a company on the Companies screen |
| Cannot log in | Wrong credentials or wrong company selected | Verify the company selector and credentials; reset via Admin if needed |
| Printing does nothing | No printer configured in macOS | Add a printer in System Settings → Printers & Scanners |
| Backup fails | Invalid or unwritable backup path | Set a valid, writable Backup path in Settings |
| Data missing after reinstall | Wrong user account | Data is per-user under `~/Library/Application Support/eCheque_MICO360/` |

---

## 10. Uninstall

1. Quit eCheque MICO360.
2. Remove the application:
   ```bash
   rm -rf "/Applications/eCheque MICO360.app"
   ```
3. **(Optional) Remove application data** — this permanently deletes all companies, cheques, and settings:
   ```bash
   rm -rf ~/Library/Application\ Support/eCheque_MICO360/
   ```

> **Back up first** if you may need the data again. Deleting the data folder is irreversible.

---

*© MICO360 Softwares — eCheque MICO360. This guide covers the macOS (Avalonia) edition.*
