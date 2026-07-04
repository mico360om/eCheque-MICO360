# eCheque MICO360 — Architecture & How It Works

**Cheque-printing + management** desktop app for Oman (currency OMR; 1000 Baisa = 1 Rial), Windows-only,
with an optional **central sync server** so several PCs share one dataset. .NET 9. This document is the
complete map of how the system is built and behaves.

---

## 1. Solution structure (6 projects)

| Project | Target | Role |
|---|---|---|
| `eCheque.MICO360` | WPF, net9.0-windows | **The desktop app** (Services / ViewModels / Views / Models, MVVM). |
| `Server` | ASP.NET Core (Kestrel) | **Sync server** → `eCheque.MICO360.Server.exe`. |
| `Sync.Contracts` | lib, net9.0 | Dependency-free wire DTOs shared by client + server (one source of truth). |
| `Sync.Client` | lib, net9.0 | **Generic sync engine** (pull / apply / push) used by the app + tests. |
| `Tests` | xUnit | 57 tests — amount-in-words, cheque logic, real encrypted-DB migration. |
| `Sync.Tests` | xUnit | 15 tests — end-to-end sync through the real server. |

Desktop app internals: ~20 Services, ~16 ViewModels, ~17 Views, ~9 Models (MVVM).

---

## 2. Data model

All storage is **SQLite** — no external database engine.

### Client (on each PC, encrypted with SQLCipher)
- `companies.db` — the **master** database.
- `company_<id>.db` — one **per-company** database.

| Tier | Database | Tables |
|---|---|---|
| Master | `companies.db` | `Companies`, `Users`, `MasterSettings`, `AuditLogs`, `BugReports` |
| Per-company | `company_<id>.db` | `ChequeProfiles`, `ChequeRecords`, `Banks`, `Payees`, `AppSettings`, `PrintHistory`, `AuditLogs` |

### Server (central)
- One `server.db` (WAL mode). Every synced row is stored in a single `SyncRows` table keyed by
  `(Entity, CompanyId, SyncId)` with a globally monotonic `ServerVersion`. Also `Devices` (registered PCs +
  tokens), `Conflicts` (audit log), `Seq` (the version counter).
- Data access is behind the `IServerStore` interface, so SQLite can be swapped for SQL Server without
  touching the HTTP endpoints.

---

## 3. Desktop app — how it works

1. **Login** — per-user accounts, passwords stored as bcrypt hashes. Roles: **Admin**, **Accountant**,
   **read-only**. "Remember me" keeps a session up to 30 days of inactivity.
2. **Active company** — a dropdown (top-right) switches companies. Switching rebinds the database to that
   company's file and every screen is rebuilt fresh, so all data is scoped to the active company (no leakage).
3. **Left menu** (role-gated into four sections):
   - **MAIN** — Dashboard · New Cheque · Cheque History · Cheque Tracking · Print History
   - **CONFIGURATION** — Cheque Profiles · My Profile · Settings
   - **ADMINISTRATION** (admin only) — Companies · Users · Audit Log
   - **HELP & LEGAL** — Check for Updates · Terms · Privacy · About
   Non-admins don't see Administration; read-only users can't open New Cheque; access is enforced again at the
   navigation layer (defense in depth).
4. **Core workflow**: create a cheque → choose a bank **profile** (layout + account) → the amount auto-converts
   to words (Omani Rials / Baisa, configurable case + wording) → **print at exact 1:1 mm** onto the pre-printed
   cheque. Alignment tools: a **Calibration Sheet** (ruler + crosshairs) and per-profile X/Y **print offset**.
5. **Tracking & reconciliation**: mark cheques Presented / Cleared / Bounced; PDC register; totals; **CSV export**.
6. **Reminders**: post-dated-cheque **email** reminders (Mailjet, user-set frequency) and **WhatsApp**
   click-to-send.
7. **Updates**: in-app updater checks GitHub Releases and runs the installer.

---

## 4. Sync — architecture

Optional; off by default. Enable per PC in **Settings → Cloud Sync** by entering the server URL and clicking
Connect. The sidebar shows a live status dot (🟢 Connected / 🔴 Disconnected / 🟠 Not connected / ⚪ Local only).

### Identity (why nothing duplicates or is lost)
- Every syncable row carries a **`SyncId` (GUID)** — the identity across all PCs. Local autoincrement `Id`s are
  never used for sync. Natural-key tables (`AppSettings`/`MasterSettings` by Key, `Payees` by Name) merge by
  their key. So the same logical row is one row everywhere.

### Change tracking (why all edits are captured)
- SQLite **triggers** (created by the migration) stamp `SyncId` + `UpdatedAtUtc` + `Dirty=1` on every
  INSERT/UPDATE — so **all** app write paths are tracked without changing the app's CRUD code.
- A one-row `_SyncGuard` flag, raised by the sync engine inside its write transaction, exempts sync's own
  writes so applied rows aren't re-marked dirty (no echo loop). Safe against UI writes because SQLite
  serialises writers.

### The cycle (startup, every ~60 s, and on demand)
1. **Pull** — send per-table cursors; the server returns rows with `ServerVersion` greater than each cursor
   (a cheap indexed range scan → minimal server load). Apply to non-dirty local rows; advance cursors.
2. **Push** — send `Dirty=1` rows in **500-row chunks**; the server upserts by `SyncId`, assigns a new
   `ServerVersion`, and returns per-row results. Clear `Dirty` (guarded so a concurrent edit isn't clobbered).
- A lightweight `GET /api/health` runs every ~20 s between full syncs to keep the status indicator fresh.

### Conflicts, retries, offline
- **Conflict**: two PCs edit the same row → **last-write-wins by `UpdatedAtUtc`**, logged server-side; the
  loser reconciles automatically.
- **Retry**: exponential backoff; every write is an idempotent upsert keyed by `SyncId`, so replays never
  duplicate. Permanent 4xx fail fast; 5xx/network retry.
- **Offline**: the app works fully on the local encrypted DB; a failed cycle just retries next tick.
- **Tiers**: the master tier (companies/users/master settings) and the active company's data both sync each
  cycle.

### Sync protocol (HTTP + JSON)
| Endpoint | Purpose |
|---|---|
| `GET /api/health` | Liveness + version. |
| `GET /` | Human status page (devices, rows). |
| `POST /api/register` | Register this PC → returns a device **bearer token** (single-org: no key). |
| `POST /api/sync/pull` | Delta pull (bearer token required). |
| `POST /api/sync/push` | Push local changes; conflict resolution (bearer token required). |

---

## 5. Deployment

Two **self-contained GUI installers** (no .NET needed on the target), in `installer/`:
- **Client** — `eCheque-MICO360-Setup-1.1.0.exe` → installs to Program Files, adds shortcuts + uninstaller.
- **Server** — `eCheque-MICO360-Server-Setup-1.1.0.exe` → asks for a **port**, installs as the **eChequeSync
  Windows service** (auto-start + auto-restart), opens the firewall, starts it. Config is written to
  `echeque.server.json` next to the EXE; the database lives in `C:\ProgramData\eCheque MICO360 Server`.

### This deployment's security model (chosen)
- **Single-organisation server** — clients connect by URL only; there is **no organisation key**.
- **TLS: self-signed certificate.** The server runs HTTPS with a self-signed cert; that cert must be installed
  in the **Trusted Root** store on **each client PC** (the client validates certificates normally, so an
  untrusted cert is rejected). Clients then use the `https://84.247.142.2:5210` URL.
- **Access control: firewall IP allowlist.** Because there is no app-level key, the firewall is the barrier —
  open the server port **only** to the known client-PC IPs, never to the whole internet.
- Client databases remain SQLCipher-encrypted at rest; sync auth is device-token based (no passwords on the wire).

---

## 6. Build, test, release

- **Build**: `dotnet build -c Release` per project (all target .NET 9).
- **Tests**: 72 total — `Tests` (57) + `Sync.Tests` (15, end-to-end through the real server). All green.
- **Release**: push a `vX.Y.Z` git tag → GitHub Actions (`.github/workflows/release.yml`) builds both
  installers with their SHA256s and publishes a GitHub Release. The client's in-app updater consumes it.
- **Current version**: 1.1.0.
