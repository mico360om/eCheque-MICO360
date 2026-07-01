# eCheque MICO360 — Feature Comparison: Windows vs macOS

**Product:** eCheque MICO360 — MICO360 Softwares
**Windows edition:** WPF (.NET 9)
**macOS edition:** Avalonia UI (.NET 9)
**Shared:** cross-platform **Core** library (business logic identical on both platforms)

**Legend**
- ✅ **Same** — identical behavior/implementation across platforms
- ⚠️ **Platform-adapted** — same feature, platform-native implementation
- 🕒 **Pending** — not yet completed / awaiting on-device validation

---

## Login & Companies

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Multi-company selector on login | ✅ | ✅ | Same Core logic |
| BCrypt password verification | ✅ | ✅ | Shared Core |
| Companies CRUD (multi-company) | ✅ | ✅ | |
| Per-company database | ✅ | ⚠️ | SQLite per company; macOS stores under `~/Library/Application Support/eCheque_MICO360/` |

## Dashboard

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Stats cards (total/printed/pending/cancelled/voided) | ✅ | ✅ | |
| Today / month counts | ✅ | ✅ | |
| Monthly & yearly value | ✅ | ✅ | |
| Recent cheques | ✅ | ✅ | |
| Quick actions | ✅ | ✅ | |

## New Cheque

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Profile auto-fill | ✅ | ✅ | |
| Payee autocomplete | ✅ | ✅ | |
| Amount + amount-in-words auto-convert | ✅ | ✅ | Shared OMR/Baisa converter |
| Memo & reference details | ✅ | ✅ | |
| Duplicate cheque-number check | ✅ | ✅ | Shared Core |
| Save Draft / Preview / Cancel | ✅ | ✅ | |
| Print | ⚠️ | ⚠️ | Windows print pipeline vs **native macOS print**; same field positioning |

## Cheque History

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Search / filter by status & date | ✅ | ✅ | |
| Edit (printed = Admin only) | ✅ | ✅ | Role rule in Core |
| Print / Cancel / Void | ✅ | ⚠️ | Print uses native macOS printing |
| Cancelled/Void cannot print | ✅ | ✅ | Shared rule |
| Export | ⚠️ | ⚠️ | Native file/folder pickers per platform |

## Print History

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Filters | ✅ | ✅ | |
| Excel export | ⚠️ | ⚠️ | Same data; native save dialog on macOS |
| Empty state | ✅ | ✅ | |

## Cheque Profiles & Layout Designer

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Visual layout designer | ✅ | ⚠️ | Same designer; rendered with Avalonia on macOS |
| X/Y coordinates | ✅ | ✅ | |
| Width/height (mm) | ✅ | ✅ | |
| Font selection | ⚠️ | ⚠️ | Uses system-available fonts per OS |

## Settings

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Company, currency (OMR), date format | ✅ | ✅ | |
| Amount-in-words options (wording/case/Baisa/Only) | ✅ | ✅ | Shared Core |
| PDF path | ⚠️ | ⚠️ | Native folder picker; macOS default under app support |
| Backup path | ⚠️ | ⚠️ | Native folder picker |
| Backup & Restore | ✅ | ✅ | Same logic; native pickers |

## Users & Roles

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Users CRUD | ✅ | ✅ | |
| Roles (Admin/Accountant/Viewer) enforcement | ✅ | ✅ | Shared Core |
| Validation | ✅ | ✅ | |
| Last-admin protection | ✅ | ✅ | |

## Audit Log

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Audit logging | ✅ | ✅ | |
| Filters | ✅ | ✅ | |
| Mandatory reprint reason logged | ✅ | ✅ | |

## Help & Legal / Updates

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Terms / Privacy / About | ✅ | ✅ | |
| Check for Updates (GitHub Releases) | ⚠️ | ⚠️ | Same source; delivery is `.exe`/installer on Windows vs `.dmg`/`.pkg` on macOS |

## Security & Data-at-Rest

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| SQLCipher database encryption | ✅ | ✅ | Shared Core |
| BCrypt password hashing | ✅ | ✅ | Shared Core |
| Encryption key protection | ⚠️ | ⚠️ | Windows **DPAPI** → macOS **Keychain / file-based key store** |
| Role-based access control | ✅ | ✅ | Shared Core |

## Packaging & Distribution

| Feature | Windows (WPF) | macOS (Avalonia) | Notes |
|---------|:-------------:|:----------------:|-------|
| Install format | ⚠️ | ⚠️ | Windows installer vs macOS `.dmg`/`.pkg` or run-from-source |
| Code signing / trust | ⚠️ | ⚠️ | macOS Gatekeeper; notarization 🕒 pending (interim `xattr` workaround) |
| Auto-update channel | ✅ | ✅ | Both pull from the same GitHub Releases repo |

---

## Summary of Intentional Platform Differences

The Windows and macOS editions are **feature-identical at the business level** because they share the same Core library. The only differences are deliberate platform adaptations:

1. **Printing & PDF** — macOS uses the **native macOS print system**; field positioning (X/Y, mm sizing) is identical.
2. **File/folder pickers** — PDF path, backup path, and export dialogs use **native macOS pickers**.
3. **Encryption key protection** — Windows uses **DPAPI**; macOS uses the **Keychain / file-based key store**. Database encryption (SQLCipher) and password hashing (BCrypt) are identical.
4. **Data location** — macOS stores per-company SQLite databases under `~/Library/Application Support/eCheque_MICO360/`.
5. **Packaging & trust** — macOS ships as `.dmg`/`.pkg` (or run-from-source) and is subject to **Gatekeeper**; **notarization is pending**, with an interim `xattr -dr com.apple.quarantine` workaround.
6. **Fonts** — the layout designer uses fonts available on the host OS.

All items marked 🕒 (notarization) are the only outstanding gaps to full parity; every core workflow is present on both platforms.

---

*© MICO360 Softwares — eCheque MICO360.*
