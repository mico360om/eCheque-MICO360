# eCheque MICO360 — Client/Server Sync: Deployment & Operations

This document covers the server-based version of eCheque MICO360: a central **sync server** plus the
**desktop client** that keeps a local encrypted copy for offline use and syncs with the server.

## 1. What you get

| Deliverable | Installer | Notes |
|---|---|---|
| **Client** | `installer/eCheque-MICO360-Setup-1.1.0.exe` | GUI installer → Program Files, shortcuts, uninstaller. |
| **Server** | `installer/eCheque-MICO360-Server-Setup-1.1.0.exe` | GUI installer → prompts for port, installs a Windows service, opens firewall, starts it. |

Both require administrator (UAC). No .NET install is needed on the target — the runtime is bundled.
The raw self-contained EXEs are also under `dist/` if you prefer a manual/portable setup.

## 2. Architecture (how it stays correct)

- **Every syncable row has a `SyncId` (GUID)** — the identity across all PCs. Local autoincrement `Id`s are
  never used for sync, so two PCs can never create "the same row twice". Natural-key tables (settings, payees,
  banks by name) merge by their key, so independent PCs converge to one row.
- **Server assigns a monotonic `ServerVersion`** to every change. Clients pull "everything newer than the
  version I already have" — an indexed range scan, so the server stays light even with many clients.
- **Change tracking**: the client marks edited rows `Dirty`; a per-table cursor records the last server version
  seen. Only deltas move in either direction.
- **Conflict handling**: if two PCs edit the same row, the server resolves **last-write-wins by edit time**
  (`UpdatedAtUtc`), logs the conflict, and returns the winning row so the loser reconciles automatically.
- **Retry**: all calls retry with exponential backoff; every write is an **upsert keyed by `SyncId`**, so a
  retried/duplicated request is a no-op — never a duplicate row.
- **Offline**: the desktop app works fully offline against its local encrypted DB; sync runs on startup, every
  60 s in the background, and on demand — a failed cycle just retries next tick.
- **Tiers**: master data (companies, users, master settings) syncs under the reserved company id 0; each
  company's cheque data (profiles, cheques, banks, payees, settings) syncs under its own id. The client syncs
  the master tier plus the **active company** each cycle (a company syncs whenever it is the one in use).

## 3. Server setup

### 3.1 Configuration (environment variables)

| Variable | Purpose | Default |
|---|---|---|
| `ECHEQUE_SERVER_DB` | Path to the server SQLite database. | `./data/server.db` next to the EXE |
| `ASPNETCORE_URLS` | Address/port(s) to listen on. | `http://0.0.0.0:5210` |

This is a **single-organisation** server: clients connect by URL only — there is no organisation key.
Because registration is open to anyone who can reach the port, **restrict access at the network layer**
(TLS + a firewall allowlist of your client IPs) — see §3.4/§3.5.

### 3.2 Run it

```
set ECHEQUE_SERVER_DB=C:\eChequeServer\server.db
set ASPNETCORE_URLS=http://0.0.0.0:5210
eCheque.MICO360.Server.exe
```

On start it prints the listen URL. Open `http://<server>:5210/` for a live status page
(devices registered, rows synced). Health check: `GET /api/health`.

### 3.3 Install as a Windows service (recommended for production)

**Easiest — the GUI installer.** Run `eCheque-MICO360-Server-Setup-1.1.0.exe` on the server (admin/UAC). It
asks for the **port**, installs to Program Files, writes the config, registers the
**eChequeSync** Windows service (auto-start + auto-restart), opens the firewall, and starts it. Uninstall from
Add/Remove Programs removes the service and firewall rule. The database lives in
`C:\ProgramData\eCheque MICO360 Server\server.db`.

**Alternative — PowerShell.** A one-shot script also ships alongside the server EXE:

1. Copy `eCheque.MICO360.Server.exe`, `Install-Server.ps1`, `Uninstall-Server.ps1` into one folder on the server.
2. (Optional) change `$Port` at the top of `Install-Server.ps1`.
3. Open **PowerShell as Administrator** in that folder and run:
   ```powershell
   .\Install-Server.ps1
   ```

It creates `C:\eCheque\Server` (EXE + `echeque.server.json` config) and `C:\eCheque\Data` (the database),
registers the **eChequeSync** service (auto-start + auto-restart on failure), opens the firewall port, starts
the service, and prints a health check. To remove: `.\Uninstall-Server.ps1`.

Config lives in `C:\eCheque\Server\echeque.server.json` (DB path, listen URL) — edit it and restart
the service (`Restart-Service eChequeSync`) to change settings. A config **file** is used rather than
environment variables because a freshly-created Windows service does not reliably pick up newly-set machine
env vars.

### 3.4 TLS (do this for anything beyond a trusted LAN)

The server speaks plain HTTP by default. For production put **HTTPS** in front, either by:
- a reverse proxy (IIS / Nginx / Caddy) terminating TLS and forwarding to `http://localhost:5210`, or
- binding Kestrel directly to an `https://` URL with a certificate (`ASPNETCORE_URLS=https://0.0.0.0:5210`
  plus the standard `ASPNETCORE_Kestrel__Certificates__Default__*` settings).

Clients then use the `https://…` URL. **Never send cheque data over plain HTTP across the internet.**

### 3.5 Enrollment secret, firewall & backups

- **Set an enrollment secret** (the installer prompts for it, or set `ECHEQUE_REGISTER_SECRET` in
  `echeque.server.json` and restart the service). When set, a PC must present the exact secret the first time
  it connects, so reaching `/api/register` is **not** enough to obtain a token. This is defence-in-depth on top
  of the firewall — strongly recommended on a public IP. Distribute the secret to your operators out-of-band
  (not in email/chat). The startup banner prints whether enrollment is PROTECTED or OPEN.
- **The firewall is still your primary access control.** Open the port **only** to your known client IPs (an
  allowlist), not to the whole internet.
- **On plain HTTP the secret, tokens and cheque data cross the wire in cleartext.** Use TLS (§3.4) for anything
  beyond a fully trusted, isolated network.
- The whole server state is the single `server.db` file — back it up on a schedule (it is WAL-mode SQLite;
  copy `server.db`, `server.db-wal`, `server.db-shm` together, or stop the service first).

## 4. Client setup (per PC)

1. Install / run `eCheque.MICO360.exe` and sign in.
2. **Settings → Cloud Sync**: enter the **Server URL** (e.g. `https://sync.mycompany.com:5210`),
   tick **Enable cloud sync**, click **Connect this PC**, then **Save**.
3. That's it — the PC registers (gets its own device token) and starts syncing in the background. Use
   **Sync now** to force a cycle; the status line shows the last result. The sidebar shows the live
   connection status.

## 5. Multi-PC rollout (avoiding first-time duplicates)

Natural-key data (settings, payees, banks) dedupes automatically. **Profiles and cheque records are matched by
`SyncId`, so data that already existed *before* sync was turned on cannot be auto-deduplicated across PCs.**
Recommended rollout:

1. Stand up the server.
2. Pick **one "primary" PC** that holds the correct existing data. Connect it first and let it finish a full
   sync — it seeds the server.
3. Connect the other PCs. Ideally they start empty (fresh installs) and pull the shared data down. If a
   secondary PC already has its own pre-sync cheques/profiles, those upload as additional rows (union, not
   merge) — clean them up once, or start those PCs fresh.

After the initial seed, ongoing multi-PC operation is fully automatic and duplicate-free.

## 6. What has been tested vs. what you must verify on your hardware

**Automatically tested (in this repo, `Sync.Tests`, all passing):** through the *real* server, with two
simulated PCs — create propagates A→B, concurrent edits resolve last-write-wins and converge, deletes
propagate as tombstones, repeated/retried sync causes **no duplication**, and independent same-name rows merge
to one. The standalone server EXE was launched and verified (health, status page, registration, push/pull,
conflict, idempotent replay, tombstone).

**You must verify in your environment (cannot be tested from the build machine):**
- Real multi-PC run over your LAN/WAN with the actual encrypted company databases.
- TLS certificate setup and the `https://` client URL.
- Firewall/port reachability from each client PC.
- Load/scale at your real client count (the SQLite server suits small/medium teams; for large scale, the data
  layer is behind `IServerStore` and can be swapped to SQL Server without touching the endpoints).
- Backup/restore drill of `server.db`.

## 6a. Design notes & known limitations (from the security review)

- **One server = one organisation.** Every registered PC can access all of that org's companies (matching the
  desktop app's company switcher). Do **not** point two different organisations at one server instance —
  `CompanyId` partitions data, it is not a tenant wall.
- **No organisation key — the network is your access control.** Registration is open to anyone who can reach
  the server port. You **must** restrict access with TLS + a firewall IP allowlist (see §3.4/§3.5); do not
  expose the port to the open internet.
- **User login state syncs, volatile fields don't.** User accounts + password *hashes* sync so staff can log
  in on any PC; last-login / failed-attempt / lockout fields are kept per-PC and never sync. Because hashes
  cross the wire, **use TLS** (section 3.4).
- **Deletes:** the app never hard-deletes records — it deactivates (banks/profiles via IsActive) or changes
  status (cheques: Cancelled/Void), which are edits and **do** sync across PCs. Only a manual hard-delete of a
  natural-key row (payee/setting/bank) directly in the database would not propagate; that isn't a user action.
- **Conflicts are last-write-wins by edit time and are logged** in the server's `Conflicts` table for audit.

## 7. Security summary

- Client databases remain **SQLCipher-encrypted at rest**; the server DB should be protected by host security
  + TLS in transit.
- **Registration is open (single-organisation server).** Every sync call still requires a device bearer token,
  but any PC that can reach the server can obtain one — so access control is the **network** (TLS + firewall
  allowlist), not an app-level key.
- No passwords are transmitted for sync; auth is device-token based.

## 8. Operations

- **Status**: `http://<server>:5210/` and `GET /api/health`.
- **Conflicts**: logged server-side in the `Conflicts` table of `server.db` (who lost, when) for audit.
- **A client isn't syncing?** Check: sync enabled + connected (Settings shows "registered"), server reachable
  (open the status page from that PC's browser), correct URL/port, TLS trust.
