using System.Security.Cryptography;
using eCheque.MICO360.Sync.Contracts;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Server
{
    /// <summary>Server-side persistence + sync engine. Swap this implementation (e.g. SQL Server) without
    /// touching the HTTP endpoints — they depend only on this interface.</summary>
    public interface IServerStore
    {
        void Initialize(string? orgKeyOverride = null);
        string OrgKey { get; }
        (string deviceId, string token) RegisterDevice(string deviceName, string machineId);
        bool ValidateToken(string? token, out string deviceId);
        void TouchDevice(string deviceId);
        PullResponse Pull(PullRequest req);
        PushResponse Push(PushRequest req);
        long RowCount();
        int DeviceCount();
    }

    /// <summary>
    /// SQLite-backed store. All synced rows live in one table keyed by (Entity, CompanyId, SyncId) with a
    /// globally monotonic ServerVersion, so pulls are cheap indexed range scans and identity never collides
    /// across PCs. Conflicts resolve last-write-wins by UpdatedAtUtc and are logged.
    /// </summary>
    public sealed class SqliteServerStore : IServerStore
    {
        readonly string _cs;
        readonly object _writeLock = new();   // serialize version allocation + writes (SMB scale; SQLite is single-writer anyway)
        string _orgKey = "";

        public SqliteServerStore(string dbPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
            _cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        }

        public string OrgKey => _orgKey;

        SqliteConnection Open()
        {
            var c = new SqliteConnection(_cs);
            c.Open();
            using (var pragma = c.CreateCommand()) { pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;"; pragma.ExecuteNonQuery(); }
            return c;
        }

        public void Initialize(string? orgKeyOverride = null)
        {
            using var c = Open();
            Exec(c, @"
                CREATE TABLE IF NOT EXISTS Meta(Key TEXT PRIMARY KEY, Value TEXT);
                CREATE TABLE IF NOT EXISTS Seq(Id INTEGER PRIMARY KEY CHECK(Id=1), V INTEGER NOT NULL);
                INSERT OR IGNORE INTO Seq(Id,V) VALUES(1,0);
                CREATE TABLE IF NOT EXISTS Devices(
                    DeviceId TEXT PRIMARY KEY, Token TEXT NOT NULL, DeviceName TEXT, MachineId TEXT,
                    CreatedUtc TEXT, LastSeenUtc TEXT);
                CREATE TABLE IF NOT EXISTS SyncRows(
                    Entity TEXT NOT NULL, CompanyId INTEGER NOT NULL, SyncId TEXT NOT NULL,
                    ServerVersion INTEGER NOT NULL, UpdatedAtUtc TEXT NOT NULL, Deleted INTEGER NOT NULL DEFAULT 0,
                    PayloadJson TEXT NOT NULL,
                    PRIMARY KEY(Entity, CompanyId, SyncId));
                CREATE INDEX IF NOT EXISTS IX_SyncRows_Version ON SyncRows(Entity, CompanyId, ServerVersion);
                CREATE TABLE IF NOT EXISTS Conflicts(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT, Entity TEXT, CompanyId INTEGER, SyncId TEXT,
                    ServerVersion INTEGER, Winner TEXT, LoserPayloadJson TEXT, DetectedUtc TEXT);");

            // Org key: explicit override (from config) wins, then env var; otherwise generate + persist once
            // so the value is stable across restarts.
            var envKey = string.IsNullOrWhiteSpace(orgKeyOverride)
                ? Environment.GetEnvironmentVariable("ECHEQUE_ORG_KEY")
                : orgKeyOverride;
            _orgKey = string.IsNullOrWhiteSpace(envKey) ? GetMeta(c, "orgkey") : envKey.Trim();
            if (string.IsNullOrWhiteSpace(_orgKey))
            {
                _orgKey = NewToken();
                SetMeta(c, "orgkey", _orgKey);
            }
            else if (string.IsNullOrWhiteSpace(GetMeta(c, "orgkey")))
                SetMeta(c, "orgkey", _orgKey);
        }

        // ---- devices / auth ----

        public (string deviceId, string token) RegisterDevice(string deviceName, string machineId)
        {
            lock (_writeLock)
            {
                using var c = Open();
                // Idempotent by MachineId: the same PC re-registering keeps one device row (and gets a fresh token).
                string? deviceId = null;
                if (!string.IsNullOrWhiteSpace(machineId))
                    using (var f = c.CreateCommand())
                    {
                        f.CommandText = "SELECT DeviceId FROM Devices WHERE MachineId=@m LIMIT 1";
                        f.Parameters.AddWithValue("@m", machineId);
                        deviceId = f.ExecuteScalar() as string;
                    }
                deviceId ??= Guid.NewGuid().ToString("N");
                var token = NewToken();
                using var up = c.CreateCommand();
                up.CommandText = @"INSERT INTO Devices(DeviceId,Token,DeviceName,MachineId,CreatedUtc,LastSeenUtc)
                                   VALUES(@id,@t,@n,@m,@now,@now)
                                   ON CONFLICT(DeviceId) DO UPDATE SET Token=@t, DeviceName=@n, LastSeenUtc=@now";
                up.Parameters.AddWithValue("@id", deviceId);
                up.Parameters.AddWithValue("@t", token);
                up.Parameters.AddWithValue("@n", deviceName ?? "");
                up.Parameters.AddWithValue("@m", machineId ?? "");
                up.Parameters.AddWithValue("@now", NowUtc());
                up.ExecuteNonQuery();
                return (deviceId, token);
            }
        }

        public bool ValidateToken(string? token, out string deviceId)
        {
            deviceId = "";
            if (string.IsNullOrWhiteSpace(token)) return false;
            using var c = Open();
            using var f = c.CreateCommand();
            f.CommandText = "SELECT DeviceId FROM Devices WHERE Token=@t LIMIT 1";
            f.Parameters.AddWithValue("@t", token);
            var id = f.ExecuteScalar() as string;
            if (id == null) return false;
            deviceId = id;
            return true;
        }

        public void TouchDevice(string deviceId)
        {
            try
            {
                using var c = Open();
                using var u = c.CreateCommand();
                u.CommandText = "UPDATE Devices SET LastSeenUtc=@now WHERE DeviceId=@id";
                u.Parameters.AddWithValue("@now", NowUtc());
                u.Parameters.AddWithValue("@id", deviceId);
                u.ExecuteNonQuery();
            }
            catch { }
        }

        // ---- pull ----

        public PullResponse Pull(PullRequest req)
        {
            var resp = new PullResponse();
            using var c = Open();
            int perEntity = Math.Clamp(req.MaxBatch <= 0 ? 500 : req.MaxBatch, 1, 2000);

            foreach (var (entity, cursor) in req.Cursors)
            {
                using var q = c.CreateCommand();
                q.CommandText = @"SELECT SyncId, ServerVersion, UpdatedAtUtc, Deleted, PayloadJson
                                  FROM SyncRows WHERE Entity=@e AND CompanyId=@c AND ServerVersion>@v
                                  ORDER BY ServerVersion LIMIT @lim";
                q.Parameters.AddWithValue("@e", entity);
                q.Parameters.AddWithValue("@c", req.CompanyId);
                q.Parameters.AddWithValue("@v", cursor);
                q.Parameters.AddWithValue("@lim", perEntity);
                long maxV = cursor; int n = 0;
                using var r = q.ExecuteReader();
                while (r.Read())
                {
                    long v = r.GetInt64(1);
                    resp.Changes.Add(new ServerChange
                    {
                        Entity = entity,
                        SyncId = r.GetString(0),
                        ServerVersion = v,
                        UpdatedAtUtc = r.GetString(2),
                        Deleted = r.GetInt64(3) != 0,
                        PayloadJson = r.GetString(4)
                    });
                    if (v > maxV) maxV = v;
                    n++;
                }
                resp.NextCursors[entity] = maxV;
                if (n >= perEntity) resp.HasMore = true; // client should pull again to drain the rest
            }
            return resp;
        }

        // ---- push ----

        public PushResponse Push(PushRequest req)
        {
            var resp = new PushResponse();
            lock (_writeLock)
            {
                using var c = Open();
                using var tx = c.BeginTransaction();
                foreach (var ch in req.Changes)
                    resp.Results.Add(ApplyOne(c, tx, req.CompanyId, ch));
                tx.Commit();
            }
            return resp;
        }

        PushResult ApplyOne(SqliteConnection c, SqliteTransaction tx, int companyId, ChangeItem ch)
        {
            var res = new PushResult { Entity = ch.Entity, SyncId = ch.SyncId };

            // Current server row for this identity, if any.
            long? curVer = null; string curUpdated = "", curPayload = "";
            using (var f = c.CreateCommand())
            {
                f.Transaction = tx;
                f.CommandText = "SELECT ServerVersion, UpdatedAtUtc, PayloadJson FROM SyncRows WHERE Entity=@e AND CompanyId=@c AND SyncId=@s";
                f.Parameters.AddWithValue("@e", ch.Entity);
                f.Parameters.AddWithValue("@c", companyId);
                f.Parameters.AddWithValue("@s", ch.SyncId);
                using var r = f.ExecuteReader();
                if (r.Read()) { curVer = r.GetInt64(0); curUpdated = r.GetString(1); curPayload = r.GetString(2); }
            }

            if (curVer == null)
            {
                long v = NextVersion(c, tx);
                Upsert(c, tx, companyId, ch, v);
                res.Status = PushStatus.Applied; res.ServerVersion = v;
                return res;
            }

            // Idempotent replay: the SAME edit already stored (same timestamp AND identical payload) — no-op.
            // A different payload sharing a timestamp is NOT idempotent; it falls through to conflict resolution
            // so it can never be silently dropped (timestamps can collide, e.g. during migration backfill).
            if (curUpdated == ch.UpdatedAtUtc && curPayload == (ch.PayloadJson ?? ""))
            {
                res.Status = PushStatus.Applied; res.ServerVersion = curVer.Value;
                return res;
            }

            // No concurrent change since the client's base version -> clean update.
            if (curVer.Value == ch.BaseServerVersion)
            {
                long v = NextVersion(c, tx);
                Upsert(c, tx, companyId, ch, v);
                res.Status = PushStatus.Applied; res.ServerVersion = v;
                return res;
            }

            // Conflict: the server row changed since the client last saw it. Last-write-wins by UpdatedAtUtc.
            if (CompareUtc(ch.UpdatedAtUtc, curUpdated) > 0)
            {
                long v = NextVersion(c, tx);
                LogConflict(c, tx, companyId, ch.Entity, ch.SyncId, v, winner: "incoming", loserPayload: ReadPayload(c, tx, companyId, ch));
                Upsert(c, tx, companyId, ch, v);
                res.Status = PushStatus.Conflict; res.ServerVersion = v;   // incoming won
                return res;
            }
            else
            {
                // Server copy wins — return it so the client reconciles its local row.
                string serverPayload = ReadPayload(c, tx, companyId, ch);
                LogConflict(c, tx, companyId, ch.Entity, ch.SyncId, curVer.Value, winner: "server", loserPayload: ch.PayloadJson);
                res.Status = PushStatus.Conflict; res.ServerVersion = curVer.Value; res.ServerPayloadJson = serverPayload;
                return res;
            }
        }

        void Upsert(SqliteConnection c, SqliteTransaction tx, int companyId, ChangeItem ch, long version)
        {
            using var u = c.CreateCommand();
            u.Transaction = tx;
            u.CommandText = @"INSERT INTO SyncRows(Entity,CompanyId,SyncId,ServerVersion,UpdatedAtUtc,Deleted,PayloadJson)
                              VALUES(@e,@c,@s,@v,@u,@d,@p)
                              ON CONFLICT(Entity,CompanyId,SyncId) DO UPDATE SET
                                ServerVersion=@v, UpdatedAtUtc=@u, Deleted=@d, PayloadJson=@p";
            u.Parameters.AddWithValue("@e", ch.Entity);
            u.Parameters.AddWithValue("@c", companyId);
            u.Parameters.AddWithValue("@s", ch.SyncId);
            u.Parameters.AddWithValue("@v", version);
            u.Parameters.AddWithValue("@u", ch.UpdatedAtUtc);
            u.Parameters.AddWithValue("@d", ch.Deleted ? 1 : 0);
            u.Parameters.AddWithValue("@p", ch.PayloadJson ?? "");
            u.ExecuteNonQuery();
        }

        static string ReadPayload(SqliteConnection c, SqliteTransaction tx, int companyId, ChangeItem ch)
        {
            using var f = c.CreateCommand();
            f.Transaction = tx;
            f.CommandText = "SELECT PayloadJson FROM SyncRows WHERE Entity=@e AND CompanyId=@c AND SyncId=@s";
            f.Parameters.AddWithValue("@e", ch.Entity);
            f.Parameters.AddWithValue("@c", companyId);
            f.Parameters.AddWithValue("@s", ch.SyncId);
            return f.ExecuteScalar() as string ?? "";
        }

        void LogConflict(SqliteConnection c, SqliteTransaction tx, int companyId, string entity, string syncId, long version, string winner, string loserPayload)
        {
            using var l = c.CreateCommand();
            l.Transaction = tx;
            l.CommandText = @"INSERT INTO Conflicts(Entity,CompanyId,SyncId,ServerVersion,Winner,LoserPayloadJson,DetectedUtc)
                              VALUES(@e,@c,@s,@v,@w,@p,@now)";
            l.Parameters.AddWithValue("@e", entity);
            l.Parameters.AddWithValue("@c", companyId);
            l.Parameters.AddWithValue("@s", syncId);
            l.Parameters.AddWithValue("@v", version);
            l.Parameters.AddWithValue("@w", winner);
            l.Parameters.AddWithValue("@p", loserPayload ?? "");
            l.Parameters.AddWithValue("@now", NowUtc());
            l.ExecuteNonQuery();
        }

        static long NextVersion(SqliteConnection c, SqliteTransaction tx)
        {
            using var u = c.CreateCommand();
            u.Transaction = tx;
            u.CommandText = "UPDATE Seq SET V=V+1 WHERE Id=1 RETURNING V";
            return Convert.ToInt64(u.ExecuteScalar());
        }

        public long RowCount()
        {
            using var c = Open();
            using var q = c.CreateCommand();
            q.CommandText = "SELECT COUNT(*) FROM SyncRows";
            return Convert.ToInt64(q.ExecuteScalar());
        }

        public int DeviceCount()
        {
            using var c = Open();
            using var q = c.CreateCommand();
            q.CommandText = "SELECT COUNT(*) FROM Devices";
            return Convert.ToInt32(q.ExecuteScalar());
        }

        // ---- helpers ----

        static int CompareUtc(string a, string b)
        {
            // Compare as instants when parseable (robust to differing fractional-second widths); else ordinal.
            bool pa = DateTimeOffset.TryParse(a, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var da);
            bool pb = DateTimeOffset.TryParse(b, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var db);
            if (pa && pb) return da.UtcDateTime.CompareTo(db.UtcDateTime);
            return string.CompareOrdinal(a, b);
        }

        static string NowUtc() => DateTime.UtcNow.ToString("o");
        static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        static void Exec(SqliteConnection c, string sql) { using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
        static string GetMeta(SqliteConnection c, string key) { using var q = c.CreateCommand(); q.CommandText = "SELECT Value FROM Meta WHERE Key=@k"; q.Parameters.AddWithValue("@k", key); return q.ExecuteScalar() as string ?? ""; }
        static void SetMeta(SqliteConnection c, string key, string val) { using var u = c.CreateCommand(); u.CommandText = "INSERT OR REPLACE INTO Meta(Key,Value) VALUES(@k,@v)"; u.Parameters.AddWithValue("@k", key); u.Parameters.AddWithValue("@v", val); u.ExecuteNonQuery(); }
    }
}
