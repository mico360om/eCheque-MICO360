using System.Net.Http.Json;
using System.Text.Json;
using eCheque.MICO360.Sync.Contracts;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Sync.Client
{
    /// <summary>
    /// The client half of the sync protocol. For a given database (one company scope, or the master tier) it
    /// pulls server changes newer than its cursor, applies them to non-dirty rows, then pushes its own dirty
    /// rows and reconciles conflicts. Identity is the row's SyncId (a GUID) or, for natural-key tables, the key
    /// value itself — so the same logical row is one row across every PC and replays never duplicate.
    /// </summary>
    public sealed class SyncClient
    {
        static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);
        // Columns that are local-only bookkeeping and never travel in a payload (SyncId rides in the envelope).
        static readonly HashSet<string> NonPayload = new(StringComparer.OrdinalIgnoreCase)
            { "Id", "Dirty", "ServerVersion", "ProfileId", "SyncId" };

        readonly HttpClient _http;
        readonly string _base;
        readonly string _token;
        bool _guard;   // does this DB have the _SyncGuard table + change-tracking triggers? (set per scope)

        public SyncClient(HttpClient http, string baseUrl, string token)
        {
            _http = http;
            _base = baseUrl.TrimEnd('/');
            _token = token;
        }

        /// <summary>Register this device and get a bearer token. Returns null if the server is unreachable.</summary>
        public static async Task<RegisterResponse?> RegisterAsync(HttpClient http, string baseUrl, RegisterRequest req, CancellationToken ct = default)
        {
            try
            {
                using var resp = await http.PostAsJsonAsync(baseUrl.TrimEnd('/') + "/api/register", req, J, ct);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadFromJsonAsync<RegisterResponse>(J, ct);
            }
            catch { return null; }
        }

        /// <summary>Pull-then-push one database scope. Never throws — failures land in <see cref="SyncReport"/>.</summary>
        public async Task<SyncReport> SyncScopeAsync(SqliteConnection conn, int companyId, IReadOnlyList<SyncEntityDef> entities, CancellationToken ct = default)
        {
            var report = new SyncReport();
            try
            {
                _guard = TableExists(conn, "_SyncGuard");
                await PullAsync(conn, companyId, entities, report, ct);
                await PushAsync(conn, companyId, entities, report, ct);
            }
            catch (Exception ex)
            {
                report.Ok = false;
                report.Error = ex.Message;
            }
            return report;
        }

        // ---------------- pull ----------------

        async Task PullAsync(SqliteConnection conn, int companyId, IReadOnlyList<SyncEntityDef> entities, SyncReport report, CancellationToken ct)
        {
            var byName = entities.ToDictionary(e => e.Name, StringComparer.Ordinal);
            bool more = true;
            int guard = 0;
            while (more && guard++ < 1000)
            {
                var req = new PullRequest { CompanyId = companyId, MaxBatch = 500 };
                foreach (var e in entities) req.Cursors[e.Name] = GetCursor(conn, e.Name);

                var resp = await PostAsync<PullRequest, PullResponse>("/api/sync/pull", req, ct)
                           ?? throw new Exception("pull returned no response");

                var skipped = new Dictionary<string, long>(StringComparer.Ordinal); // entity -> lowest version we skipped
                RunGuarded(conn, () =>
                {
                    foreach (var ch in resp.Changes)
                    {
                        report.Pulled++;
                        if (!byName.TryGetValue(ch.Entity, out var def)) continue;
                        if (IsExcludedRow(def, ch.SyncId)) continue; // device-local row (e.g. Sync_*) — ignore, cursor advances
                        if (ApplyChange(conn, def, ch)) report.Applied++;
                        else if (!skipped.TryGetValue(ch.Entity, out var m) || ch.ServerVersion < m)
                            skipped[ch.Entity] = ch.ServerVersion; // had unpushed local edits — don't advance past it
                    }
                    foreach (var (entity, v) in resp.NextCursors)
                    {
                        long cursor = v;
                        if (skipped.TryGetValue(entity, out var minSkip) && minSkip - 1 < cursor) cursor = minSkip - 1;
                        SetCursor(conn, entity, cursor);
                    }
                });

                // If we held a cursor back for skipped rows, stop draining now — they retry next cycle after
                // push resolves the local edits (otherwise HasMore could loop on the same un-advanced cursor).
                more = resp.HasMore && skipped.Count == 0;
            }
        }

        /// <summary>Apply one server change locally. Skips rows with unpushed local edits (Dirty=1) so the
        /// authoritative resolution happens at push time; returns true if the local row was written/removed.</summary>
        bool ApplyChange(SqliteConnection conn, SyncEntityDef def, ServerChange ch)
        {
            string keyCol = def.Guid ? "SyncId" : def.NaturalKey!;
            string keyVal = ch.SyncId;

            // Protect unpushed local edits — let push reconcile them against the server.
            if (RowIsDirty(conn, def.Table, keyCol, keyVal)) return false;

            if (ch.Deleted)
            {
                Exec(conn, $"DELETE FROM {def.Table} WHERE {keyCol}=@k", ("@k", keyVal));
                return true;
            }

            var payload = Deserialize(ch.PayloadJson);
            var cols = TableColumns(conn, def.Table);

            // Column set to write: payload columns that exist locally, minus the identity/bookkeeping ones,
            // plus the resolved identity + server version. Portable FK is resolved to a local id.
            var write = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (col, val) in payload)
                if (cols.Contains(col) && !NonPayload.Contains(col) && !col.Equals("ProfileSyncId", StringComparison.OrdinalIgnoreCase))
                    write[col] = val;

            if (def.Guid) write["SyncId"] = keyVal;
            write["ServerVersion"] = ch.ServerVersion;
            write["Dirty"] = 0L;

            if (def.HasProfileFk && payload.TryGetValue("ProfileSyncId", out var psid) && psid is string ps && ps.Length > 0)
            {
                if (cols.Contains("ProfileSyncId")) write["ProfileSyncId"] = ps;
                if (cols.Contains("ProfileId")) write["ProfileId"] = ResolveProfileId(conn, ps);
            }

            UpsertRow(conn, def.Table, keyCol, keyVal, write);
            return true;
        }

        // ---------------- push ----------------

        const int PushBatch = 500; // bound each push request so large first-syncs don't send one giant payload

        async Task PushAsync(SqliteConnection conn, int companyId, IReadOnlyList<SyncEntityDef> entities, SyncReport report, CancellationToken ct)
        {
            foreach (var def in entities)
            {
                var all = ReadDirty(conn, def);
                if (all.Count == 0) continue;

                for (int off = 0; off < all.Count; off += PushBatch)
                {
                    var changes = all.GetRange(off, Math.Min(PushBatch, all.Count - off));

                    // Remember exactly what we sent, so we don't clobber an edit the user made during the round-trip.
                    var sent = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var chg in changes) sent[chg.SyncId] = chg.UpdatedAtUtc;

                    var req = new PushRequest { CompanyId = companyId, Changes = changes };
                    var resp = await PostAsync<PushRequest, PushResponse>("/api/sync/push", req, ct)
                               ?? throw new Exception("push returned no response");

                    RunGuarded(conn, () =>
                    {
                        string keyCol = def.Guid ? "SyncId" : def.NaturalKey!;
                        foreach (var r in resp.Results)
                        {
                        report.Pushed++;
                        if (r.Status == PushStatus.Conflict) report.Conflicts++;

                        // Was the row edited again locally since we read it for this push? If so, leave it dirty
                        // and reconcile on the next cycle — never overwrite a fresher local edit.
                        sent.TryGetValue(r.SyncId, out var pushedUpdated);
                        var cur = CurrentUpdatedAt(conn, def.Table, keyCol, r.SyncId);
                        if (cur != null && !string.Equals(cur, pushedUpdated, StringComparison.Ordinal)) continue;

                        if (r.Status == PushStatus.Conflict && !string.IsNullOrEmpty(r.ServerPayloadJson))
                        {
                            // Server copy won — adopt the server version locally.
                            ApplyChange(conn, def, new ServerChange
                            {
                                Entity = def.Name, SyncId = r.SyncId, ServerVersion = r.ServerVersion,
                                Deleted = false, PayloadJson = r.ServerPayloadJson!
                            }, force: true);
                            continue;
                        }
                        // Applied (or incoming won): clear dirty + record the authoritative version.
                        Exec(conn, $"UPDATE {def.Table} SET Dirty=0, ServerVersion=@v WHERE {keyCol}=@k AND UpdatedAtUtc=@u",
                            ("@v", r.ServerVersion), ("@k", r.SyncId), ("@u", pushedUpdated ?? ""));
                        }
                    });
                }
            }
        }

        // Overload that ignores the dirty guard, used when the server explicitly won a conflict.
        bool ApplyChange(SqliteConnection conn, SyncEntityDef def, ServerChange ch, bool force)
        {
            if (!force) return ApplyChange(conn, def, ch);
            if (IsExcludedRow(def, ch.SyncId)) return false; // never let a server copy overwrite device-local rows
            string keyCol = def.Guid ? "SyncId" : def.NaturalKey!;
            var payload = Deserialize(ch.PayloadJson);
            var cols = TableColumns(conn, def.Table);
            var write = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (col, val) in payload)
                if (cols.Contains(col) && !NonPayload.Contains(col) && !col.Equals("ProfileSyncId", StringComparison.OrdinalIgnoreCase))
                    write[col] = val;
            if (def.Guid) write["SyncId"] = ch.SyncId;
            write["ServerVersion"] = ch.ServerVersion;
            write["Dirty"] = 0L;
            if (def.HasProfileFk && payload.TryGetValue("ProfileSyncId", out var psid) && psid is string ps && ps.Length > 0)
            {
                if (cols.Contains("ProfileSyncId")) write["ProfileSyncId"] = ps;
                if (cols.Contains("ProfileId")) write["ProfileId"] = ResolveProfileId(conn, ps);
            }
            UpsertRow(conn, def.Table, keyCol, ch.SyncId, write);
            return true;
        }

        List<ChangeItem> ReadDirty(SqliteConnection conn, SyncEntityDef def)
        {
            var cols = TableColumns(conn, def.Table);
            var list = new List<ChangeItem>();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT * FROM {def.Table} WHERE Dirty=1";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < r.FieldCount; i++)
                    row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);

                string syncId = def.Guid ? (row.GetValueOrDefault("SyncId") as string ?? "")
                                         : (row.GetValueOrDefault(def.NaturalKey!)?.ToString() ?? "");
                if (string.IsNullOrEmpty(syncId)) continue; // cannot sync an identity-less row
                if (IsExcludedRow(def, syncId)) continue;   // device-local row (e.g. Sync_*) — never pushed

                var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (col, val) in row)
                    if (!NonPayload.Contains(col) && !(def.Exclude?.Contains(col) ?? false)) payload[col] = val;

                list.Add(new ChangeItem
                {
                    Entity = def.Name,
                    SyncId = syncId,
                    UpdatedAtUtc = row.GetValueOrDefault("UpdatedAtUtc")?.ToString() ?? DateTime.UtcNow.ToString("o"),
                    Deleted = Convert.ToInt64(row.GetValueOrDefault("Deleted") ?? 0L) != 0,
                    BaseServerVersion = Convert.ToInt64(row.GetValueOrDefault("ServerVersion") ?? 0L),
                    PayloadJson = JsonSerializer.Serialize(payload, J)
                });
            }
            return list;
        }

        // ---------------- db helpers ----------------

        readonly Dictionary<string, HashSet<string>> _colCache = new();
        HashSet<string> TableColumns(SqliteConnection conn, string table)
        {
            if (_colCache.TryGetValue(table, out var c)) return c;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({table})";
            using var r = cmd.ExecuteReader();
            while (r.Read()) set.Add(r.GetString(1));
            _colCache[table] = set;
            return set;
        }

        /// <summary>Rows whose natural key matches a def's excluded prefix are device-local and never sync,
        /// in either direction (e.g. MasterSettings "Sync_*": this PC's token / machine id / last-run state).</summary>
        static bool IsExcludedRow(SyncEntityDef def, string syncId)
        {
            if (def.ExcludeKeyPrefixes == null) return false;
            foreach (var p in def.ExcludeKeyPrefixes)
                if (syncId.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool RowIsDirty(SqliteConnection conn, string table, string keyCol, string keyVal)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT Dirty FROM {table} WHERE {keyCol}=@k";
            cmd.Parameters.AddWithValue("@k", keyVal);
            var v = cmd.ExecuteScalar();
            return v != null && v != DBNull.Value && Convert.ToInt64(v) != 0;
        }

        static void UpsertRow(SqliteConnection conn, string table, string keyCol, string keyVal, Dictionary<string, object?> write)
        {
            bool exists;
            using (var q = conn.CreateCommand())
            {
                q.CommandText = $"SELECT 1 FROM {table} WHERE {keyCol}=@k";
                q.Parameters.AddWithValue("@k", keyVal);
                exists = q.ExecuteScalar() != null;
            }

            using var cmd = conn.CreateCommand();
            if (exists)
            {
                var sets = write.Keys.Where(k => !k.Equals(keyCol, StringComparison.OrdinalIgnoreCase)).ToList();
                if (sets.Count == 0) return;
                cmd.CommandText = $"UPDATE {table} SET {string.Join(",", sets.Select(k => $"{k}=@{k}"))} WHERE {keyCol}=@key";
                foreach (var k in sets) cmd.Parameters.AddWithValue("@" + k, write[k] ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@key", keyVal);
            }
            else
            {
                var keys = write.Keys.ToList();
                if (!keys.Contains(keyCol, StringComparer.OrdinalIgnoreCase)) { keys.Add(keyCol); write[keyCol] = keyVal; }
                cmd.CommandText = $"INSERT INTO {table}({string.Join(",", keys)}) VALUES({string.Join(",", keys.Select(k => "@" + k))})";
                foreach (var k in keys) cmd.Parameters.AddWithValue("@" + k, write[k] ?? DBNull.Value);
            }
            cmd.ExecuteNonQuery();
        }

        static long ResolveProfileId(SqliteConnection conn, string profileSyncId)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id FROM ChequeProfiles WHERE SyncId=@s";
            cmd.Parameters.AddWithValue("@s", profileSyncId);
            var v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        /// <summary>Runs the local writes for a pull batch / push response inside one transaction. On DBs that have
        /// the change-tracking triggers, it also raises the _SyncGuard flag so those writes are NOT re-marked dirty
        /// (which would echo forever). Batching also makes large syncs far faster (one commit instead of per-row).</summary>
        void RunGuarded(SqliteConnection conn, Action work)
        {
            Exec(conn, "BEGIN");
            try
            {
                if (_guard) Exec(conn, "UPDATE _SyncGuard SET Active=1 WHERE Id=1");
                work();
                if (_guard) Exec(conn, "UPDATE _SyncGuard SET Active=0 WHERE Id=1");
                Exec(conn, "COMMIT");
            }
            catch
            {
                try { Exec(conn, "ROLLBACK"); } catch { } // rollback also reverts the guard flag
                throw;
            }
        }

        static bool TableExists(SqliteConnection conn, string name)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table' AND name=@n";
            cmd.Parameters.AddWithValue("@n", name);
            return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
        }

        static string? CurrentUpdatedAt(SqliteConnection conn, string table, string keyCol, string keyVal)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT UpdatedAtUtc FROM {table} WHERE {keyCol}=@k";
            cmd.Parameters.AddWithValue("@k", keyVal);
            var v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? null : v.ToString();
        }

        static long GetCursor(SqliteConnection conn, string entity)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT LastServerVersion FROM SyncState WHERE Entity=@e";
            cmd.Parameters.AddWithValue("@e", entity);
            var v = cmd.ExecuteScalar();
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        static void SetCursor(SqliteConnection conn, string entity, long version)
            => Exec(conn, "INSERT INTO SyncState(Entity,LastServerVersion) VALUES(@e,@v) ON CONFLICT(Entity) DO UPDATE SET LastServerVersion=@v",
                    ("@e", entity), ("@v", version));

        static void Exec(SqliteConnection conn, string sql, params (string, object)[] ps)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
            cmd.ExecuteNonQuery();
        }

        static Dictionary<string, object?> Deserialize(string json)
        {
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(json)) return dict;
            using var doc = JsonDocument.Parse(json);
            foreach (var p in doc.RootElement.EnumerateObject())
                dict[p.Name] = p.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => p.Value.GetString(),
                    JsonValueKind.True => 1L,
                    JsonValueKind.False => 0L,
                    JsonValueKind.Number => p.Value.TryGetInt64(out var l) ? l : p.Value.GetDouble(),
                    _ => p.Value.GetRawText()
                };
            return dict;
        }

        // ---------------- transport (retry/backoff) ----------------

        async Task<TResp?> PostAsync<TReq, TResp>(string path, TReq body, CancellationToken ct)
        {
            const int maxAttempts = 4;
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, _base + path);
                    req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _token);
                    req.Content = JsonContent.Create(body, options: J);
                    using var resp = await _http.SendAsync(req, ct);
                    if ((int)resp.StatusCode >= 500 && attempt < maxAttempts) { await Backoff(attempt, ct); continue; }
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadFromJsonAsync<TResp>(J, ct);
                }
                catch (Exception ex) when (attempt < maxAttempts && IsTransient(ex))
                {
                    await Backoff(attempt, ct);
                }
            }
        }

        // Retry network glitches, timeouts and 5xx; do NOT retry permanent 4xx (bad request / auth) — fail fast.
        static bool IsTransient(Exception ex)
            => ex is not HttpRequestException hre || hre.StatusCode is null || (int)hre.StatusCode >= 500;

        static Task Backoff(int attempt, CancellationToken ct)
            => Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), ct); // 200,400,800ms
    }
}
