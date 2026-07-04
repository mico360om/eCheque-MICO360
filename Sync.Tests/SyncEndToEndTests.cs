using eCheque.MICO360.Sync.Client;
using eCheque.MICO360.Sync.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace eCheque.MICO360.Sync.Tests
{
    // One class, run sequentially — tests share process-global env vars for the server DB path.
    [Collection("sync-serial")]
    public class SyncEndToEndTests : IDisposable
    {
        const int Company = 5;
        static readonly SyncEntityDef[] Entities =
        {
            new() { Name = SyncEntities.ChequeProfile, Table = "ChequeProfiles" },
            new() { Name = SyncEntities.Payee, Table = "Payees", Guid = false, NaturalKey = "Name" },
        };
        static readonly SyncEntityDef[] MasterEntities =
        {
            new() { Name = SyncEntities.Company, Table = "Companies" },
            new() { Name = SyncEntities.MasterSetting, Table = "MasterSettings", Guid = false, NaturalKey = "Key" },
        };

        readonly string _serverDb;
        readonly WebApplicationFactory<Program> _factory;
        readonly HttpClient _http;
        readonly string _tokenA, _tokenB;
        readonly List<string> _tempFiles = new();

        public SyncEndToEndTests()
        {
            _serverDb = NewTempFile("server");
            Environment.SetEnvironmentVariable("ECHEQUE_SERVER_DB", _serverDb);
            _factory = new WebApplicationFactory<Program>();
            _http = _factory.CreateClient();
            _tokenA = Register("PC-A", "MID-A");
            _tokenB = Register("PC-B", "MID-B");
        }

        string Register(string name, string mid)
        {
            var r = SyncClient.RegisterAsync(_http, "", new RegisterRequest { DeviceName = name, MachineId = mid }).Result;
            Assert.NotNull(r);
            Assert.False(string.IsNullOrEmpty(r!.Token));
            return r.Token;
        }

        SyncClient ClientFor(string token) => new(_http, "", token);

        // ---------- scenarios ----------

        [Fact]
        public async Task Create_on_A_propagates_to_B()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            var id = InsertProfile(a, "Bank Muscat - Std", "Bank Muscat");

            var ra = await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            Assert.True(ra.Ok, ra.Error);
            Assert.Equal(1, ra.Pushed);

            var rb = await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            Assert.True(rb.Ok, rb.Error);

            Assert.Equal("Bank Muscat - Std", ProfileName(b, id));
            Assert.Equal(0, Dirty(b, "ChequeProfiles", "SyncId", id)); // applied clean, not dirty
            Assert.Equal(1, CountProfiles(b));
        }

        [Fact]
        public async Task Concurrent_edit_resolves_last_write_wins_and_converges()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            var id = InsertProfile(a, "Original", "Bank Muscat");
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);   // both now hold the row

            // A edits later (wins), B edits earlier (loses) — pushed in that order.
            EditProfile(a, id, "Edited-by-A", "2026-07-04T12:00:00.0000000Z");
            EditProfile(b, id, "Edited-by-B", "2026-07-04T11:00:00.0000000Z");
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);   // server row = A

            var rb = await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities); // B pushes stale -> conflict
            Assert.True(rb.Conflicts >= 1);
            Assert.Equal("Edited-by-A", ProfileName(b, id)); // B reverted to the winner

            // A re-syncs and stays consistent; both converged, still a single row.
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            Assert.Equal("Edited-by-A", ProfileName(a, id));
            Assert.Equal(1, CountProfiles(a));
            Assert.Equal(1, CountProfiles(b));
        }

        [Fact]
        public async Task Delete_propagates_as_tombstone()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            var id = InsertProfile(a, "ToDelete", "NBO");
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            Assert.Equal(1, CountProfiles(b));

            Exec(a, "UPDATE ChequeProfiles SET Deleted=1, Dirty=1, UpdatedAtUtc='2026-07-04T13:00:00.0000000Z' WHERE SyncId=@k", ("@k", id));
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);

            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            Assert.Equal(0, CountProfiles(b)); // tombstone removed it on B
        }

        [Fact]
        public async Task Repeated_sync_is_idempotent_no_duplication()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            var id = InsertProfile(a, "Once", "Sohar");
            // Sync A three times (simulating retries) — must not create duplicates.
            for (int i = 0; i < 3; i++) await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);

            Assert.Equal(1, CountProfiles(a));
            Assert.Equal(1, CountProfiles(b));
            Assert.Equal("Once", ProfileName(b, id));
        }

        [Fact]
        public async Task Independent_natural_key_rows_merge_without_duplication()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            // Both PCs independently create the SAME payee name (different local origin) — must dedup by name.
            InsertPayee(a, "Acme Trading");
            InsertPayee(b, "Acme Trading");
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities); // drain echo

            Assert.Equal(1, CountPayees(a));
            Assert.Equal(1, CountPayees(b));
        }

        [Fact]
        public async Task Large_first_sync_chunks_and_loses_nothing()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            const int n = 1200; // > the 500-row push batch, so the client must chunk into multiple requests
            for (int i = 0; i < n; i++) InsertProfile(a, $"P{i:0000}", "Bank");

            var ra = await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            Assert.True(ra.Ok, ra.Error);
            Assert.Equal(n, ra.Pushed);            // every row pushed across chunks
            Assert.Equal(n, CountProfiles(a));

            var rb = await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            Assert.True(rb.Ok, rb.Error);
            Assert.Equal(n, CountProfiles(b));     // all arrived on B, no loss, no duplication
        }

        [Fact]
        public async Task Three_pcs_converge_no_loss_no_duplication()
        {
            using var a = NewClientDb(); using var b = NewClientDb(); using var c = NewClientDb();
            var tC = Register("PC-C", "MID-C");
            for (int i = 0; i < 20; i++)
            {
                InsertProfile(a, $"A{i}", "BankA");
                InsertProfile(b, $"B{i}", "BankB");
                InsertProfile(c, $"C{i}", "BankC");
            }
            // Push all three up, then pull twice each to drain everyone's changes.
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            await ClientFor(tC).SyncScopeAsync(c, Company, Entities);
            for (int pass = 0; pass < 2; pass++)
            {
                await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
                await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
                await ClientFor(tC).SyncScopeAsync(c, Company, Entities);
            }
            Assert.Equal(60, CountProfiles(a)); // 3 PCs x 20 rows, no loss
            Assert.Equal(60, CountProfiles(b)); // no duplication
            Assert.Equal(60, CountProfiles(c));
        }

        [Fact]
        public async Task Offline_edits_reconcile_when_back_online()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            var id = InsertProfile(a, "Made offline", "Bank");
            // "Offline": the row stays Dirty (unsynced). Coming online, it must reach B intact.
            Assert.Equal(1, Dirty(a, "ChequeProfiles", "SyncId", id));
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities); // back online
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            Assert.Equal("Made offline", ProfileName(b, id));
            Assert.Equal(0, Dirty(a, "ChequeProfiles", "SyncId", id)); // now clean
        }

        [Fact]
        public async Task Retry_recovers_from_transient_network_failure()
        {
            using var a = NewClientDb();
            var id = InsertProfile(a, "Through the storm", "Bank");
            // First two HTTP sends fail; the engine's backoff retries and the cycle still succeeds.
            var report = await FlakyClientFor(_tokenA, failFirst: 2).SyncScopeAsync(a, Company, Entities);
            Assert.True(report.Ok, report.Error);
            Assert.Equal(1, report.Pushed);
            Assert.Equal(0, Dirty(a, "ChequeProfiles", "SyncId", id));
        }

        [Fact]
        public async Task Persistent_failure_is_graceful_then_recovers_no_loss()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            var id = InsertProfile(a, "Resilient", "Bank");
            // Server effectively unreachable -> fails gracefully, no throw, row stays Dirty (no data loss).
            var failed = await FlakyClientFor(_tokenA, failFirst: 100).SyncScopeAsync(a, Company, Entities);
            Assert.False(failed.Ok);
            Assert.Equal(1, Dirty(a, "ChequeProfiles", "SyncId", id));
            // Next (healthy) cycle pushes it through — the edit was never lost.
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities);
            Assert.Equal("Resilient", ProfileName(b, id));
        }

        [Fact]
        public async Task Master_tier_data_syncs_across_pcs()
        {
            using var a = NewMasterDb(); using var b = NewMasterDb();
            var id = InsertCompany(a, "Acme Trading LLC");
            Exec(a, "INSERT INTO MasterSettings(Key,Value,UpdatedAtUtc,Dirty) VALUES('Mailjet_FromName','Acme',@u,1)", ("@u", DateTime.UtcNow.ToString("o")));
            await ClientFor(_tokenA).SyncScopeAsync(a, SyncEntities.MasterCompanyId, MasterEntities);
            await ClientFor(_tokenB).SyncScopeAsync(b, SyncEntities.MasterCompanyId, MasterEntities);
            Assert.Equal("Acme Trading LLC", (string?)Scalar(b, "SELECT Name FROM Companies WHERE SyncId=@s", ("@s", id)));
            Assert.Equal("Acme", (string?)Scalar(b, "SELECT Value FROM MasterSettings WHERE Key='Mailjet_FromName'"));
        }

        [Fact]
        public async Task Idle_pull_transfers_nothing_when_up_to_date()
        {
            using var a = NewClientDb();
            InsertProfile(a, "Once", "Bank");
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities); // push
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities); // drains the one-time self-echo
            var idle = await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities);
            // Minimum server load: once settled, nothing changed => nothing applied or pushed.
            Assert.True(idle.Ok);
            Assert.Equal(0, idle.Applied);
            Assert.Equal(0, idle.Pushed);
        }

        [Fact]
        public async Task Many_cycles_are_stable()
        {
            using var a = NewClientDb(); using var b = NewClientDb();
            InsertProfile(a, "Seed", "Bank");
            for (int i = 0; i < 25; i++)
            {
                Assert.True((await ClientFor(_tokenA).SyncScopeAsync(a, Company, Entities)).Ok);
                Assert.True((await ClientFor(_tokenB).SyncScopeAsync(b, Company, Entities)).Ok);
            }
            Assert.Equal(1, CountProfiles(a)); // no drift, no duplication over many cycles
            Assert.Equal(1, CountProfiles(b));
        }

        [Fact]
        public async Task Trigger_stamped_app_writes_sync_and_applied_rows_stay_clean()
        {
            // Mirrors production: the DB has change-tracking triggers + guard, and "app writes" are plain
            // INSERT/UPDATE that set NO sync columns — the triggers must stamp them; the engine's guard must
            // stop applied rows from being re-dirtied (which would echo forever).
            var e = new[] { new SyncEntityDef { Name = SyncEntities.ChequeProfile, Table = "ChequeProfiles" } };
            using var a = NewTriggeredClientDb(); using var b = NewTriggeredClientDb();

            Exec(a, "INSERT INTO ChequeProfiles(Name,BankName) VALUES('Muscat Main','Bank Muscat')"); // no SyncId!
            Assert.False(string.IsNullOrEmpty((string?)Scalar(a, "SELECT SyncId FROM ChequeProfiles WHERE Name='Muscat Main'")));
            Assert.Equal(1L, System.Convert.ToInt64(Scalar(a, "SELECT Dirty FROM ChequeProfiles WHERE Name='Muscat Main'")));

            var ra = await ClientFor(_tokenA).SyncScopeAsync(a, Company, e);
            Assert.True(ra.Ok, ra.Error);
            Assert.Equal(1, ra.Pushed);
            Assert.Equal(0L, System.Convert.ToInt64(Scalar(a, "SELECT Dirty FROM ChequeProfiles WHERE Name='Muscat Main'"))); // cleared, trigger didn't re-dirty

            var rb = await ClientFor(_tokenB).SyncScopeAsync(b, Company, e);
            Assert.True(rb.Ok, rb.Error);
            Assert.Equal("Bank Muscat", (string?)Scalar(b, "SELECT BankName FROM ChequeProfiles WHERE Name='Muscat Main'"));
            Assert.Equal(0L, System.Convert.ToInt64(Scalar(b, "SELECT Dirty FROM ChequeProfiles WHERE Name='Muscat Main'"))); // applied clean — guard worked through the engine

            var rb2 = await ClientFor(_tokenB).SyncScopeAsync(b, Company, e);
            Assert.Equal(0, rb2.Pushed); // B does not re-push a row it only received

            // A plain UPDATE on B (trigger dirties it) propagates back to A.
            Exec(b, "UPDATE ChequeProfiles SET BankName='NBO' WHERE Name='Muscat Main'");
            Assert.Equal(1L, System.Convert.ToInt64(Scalar(b, "SELECT Dirty FROM ChequeProfiles WHERE Name='Muscat Main'")));
            await ClientFor(_tokenB).SyncScopeAsync(b, Company, e);
            await ClientFor(_tokenA).SyncScopeAsync(a, Company, e);
            Assert.Equal("NBO", (string?)Scalar(a, "SELECT BankName FROM ChequeProfiles WHERE Name='Muscat Main'"));
            Assert.Equal(1, CountProfiles(a)); // no duplication
            Assert.Equal(1, CountProfiles(b));
        }

        // ---------- client-db harness (plain SQLite; the engine is encryption-agnostic) ----------

        SqliteConnection NewTriggeredClientDb()
        {
            var path = NewTempFile("tclient");
            var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            conn.Open();
            Exec(conn, @"
                CREATE TABLE _SyncGuard(Id INTEGER PRIMARY KEY CHECK(Id=1), Active INTEGER DEFAULT 0);
                INSERT INTO _SyncGuard(Id,Active) VALUES(1,0);
                CREATE TABLE ChequeProfiles(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, BankName TEXT,
                    SyncId TEXT, UpdatedAtUtc TEXT, Deleted INTEGER DEFAULT 0, Dirty INTEGER DEFAULT 0, ServerVersion INTEGER DEFAULT 0);
                CREATE TRIGGER trg_cp_si AFTER INSERT ON ChequeProfiles FOR EACH ROW WHEN (SELECT Active FROM _SyncGuard WHERE Id=1)=0
                  BEGIN UPDATE ChequeProfiles SET SyncId=CASE WHEN NEW.SyncId IS NULL OR NEW.SyncId='' THEN lower(hex(randomblob(16))) ELSE NEW.SyncId END,
                    UpdatedAtUtc=strftime('%Y-%m-%dT%H:%M:%fZ','now'), Dirty=1 WHERE rowid=NEW.rowid; END;
                CREATE TRIGGER trg_cp_su AFTER UPDATE ON ChequeProfiles FOR EACH ROW WHEN (SELECT Active FROM _SyncGuard WHERE Id=1)=0
                  BEGIN UPDATE ChequeProfiles SET UpdatedAtUtc=strftime('%Y-%m-%dT%H:%M:%fZ','now'), Dirty=1 WHERE rowid=NEW.rowid; END;
                CREATE TABLE SyncState(Entity TEXT PRIMARY KEY, LastServerVersion INTEGER DEFAULT 0);");
            return conn;
        }


        SyncClient FlakyClientFor(string token, int failFirst)
        {
            var handler = new FlakyHandler(_factory.Server.CreateHandler(), failFirst);
            var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };
            return new SyncClient(http, "", token);
        }

        SqliteConnection NewMasterDb()
        {
            var path = NewTempFile("master");
            var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            conn.Open();
            Exec(conn, @"
                CREATE TABLE Companies(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, TradeName TEXT,
                    SyncId TEXT, UpdatedAtUtc TEXT, Deleted INTEGER DEFAULT 0, Dirty INTEGER DEFAULT 0, ServerVersion INTEGER DEFAULT 0);
                CREATE TABLE MasterSettings(Key TEXT PRIMARY KEY, Value TEXT,
                    SyncId TEXT, UpdatedAtUtc TEXT, Deleted INTEGER DEFAULT 0, Dirty INTEGER DEFAULT 0, ServerVersion INTEGER DEFAULT 0);
                CREATE TABLE SyncState(Entity TEXT PRIMARY KEY, LastServerVersion INTEGER DEFAULT 0);");
            return conn;
        }

        static string InsertCompany(SqliteConnection c, string name)
        {
            var id = Guid.NewGuid().ToString("N");
            Exec(c, "INSERT INTO Companies(Name,SyncId,UpdatedAtUtc,Dirty) VALUES(@n,@s,@u,1)",
                ("@n", name), ("@s", id), ("@u", DateTime.UtcNow.ToString("o")));
            return id;
        }


        SqliteConnection NewClientDb()
        {
            var path = NewTempFile("client");
            var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
            conn.Open();
            Exec(conn, @"
                CREATE TABLE ChequeProfiles(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT, BankName TEXT,
                    SyncId TEXT, UpdatedAtUtc TEXT, Deleted INTEGER DEFAULT 0, Dirty INTEGER DEFAULT 0, ServerVersion INTEGER DEFAULT 0);
                CREATE TABLE Payees(Name TEXT PRIMARY KEY, LastUsed TEXT,
                    SyncId TEXT, UpdatedAtUtc TEXT, Deleted INTEGER DEFAULT 0, Dirty INTEGER DEFAULT 0, ServerVersion INTEGER DEFAULT 0);
                CREATE TABLE SyncState(Entity TEXT PRIMARY KEY, LastServerVersion INTEGER DEFAULT 0);");
            return conn;
        }

        static string InsertProfile(SqliteConnection c, string name, string bank)
        {
            var id = Guid.NewGuid().ToString("N");
            Exec(c, "INSERT INTO ChequeProfiles(Name,BankName,SyncId,UpdatedAtUtc,Dirty) VALUES(@n,@b,@s,@u,1)",
                ("@n", name), ("@b", bank), ("@s", id), ("@u", DateTime.UtcNow.ToString("o")));
            return id;
        }

        static void EditProfile(SqliteConnection c, string id, string name, string tsUtc)
            => Exec(c, "UPDATE ChequeProfiles SET Name=@n, UpdatedAtUtc=@u, Dirty=1 WHERE SyncId=@s", ("@n", name), ("@u", tsUtc), ("@s", id));

        static void InsertPayee(SqliteConnection c, string name)
            => Exec(c, "INSERT INTO Payees(Name,LastUsed,UpdatedAtUtc,Dirty) VALUES(@n,@l,@u,1)",
                ("@n", name), ("@l", DateTime.UtcNow.ToString("o")), ("@u", DateTime.UtcNow.ToString("o")));

        static string? ProfileName(SqliteConnection c, string id) => Scalar(c, "SELECT Name FROM ChequeProfiles WHERE SyncId=@s", ("@s", id)) as string;
        static int CountProfiles(SqliteConnection c) => Convert.ToInt32(Scalar(c, "SELECT COUNT(*) FROM ChequeProfiles WHERE Deleted=0"));
        static int CountPayees(SqliteConnection c) => Convert.ToInt32(Scalar(c, "SELECT COUNT(*) FROM Payees WHERE Deleted=0"));
        static long Dirty(SqliteConnection c, string t, string k, string v) => Convert.ToInt64(Scalar(c, $"SELECT Dirty FROM {t} WHERE {k}=@k", ("@k", v)) ?? 0L);

        static void Exec(SqliteConnection c, string sql, params (string, object)[] ps)
        { using var cmd = c.CreateCommand(); cmd.CommandText = sql; foreach (var (n, val) in ps) cmd.Parameters.AddWithValue(n, val); cmd.ExecuteNonQuery(); }
        static object? Scalar(SqliteConnection c, string sql, params (string, object)[] ps)
        { using var cmd = c.CreateCommand(); cmd.CommandText = sql; foreach (var (n, val) in ps) cmd.Parameters.AddWithValue(n, val); var v = cmd.ExecuteScalar(); return v == DBNull.Value ? null : v; }

        string NewTempFile(string prefix)
        {
            var p = Path.Combine(Path.GetTempPath(), $"echeque_it_{prefix}_{Guid.NewGuid():N}.db");
            _tempFiles.Add(p);
            return p;
        }

        public void Dispose()
        {
            _http.Dispose();
            _factory.Dispose();
            SqliteConnection.ClearAllPools();
            foreach (var f in _tempFiles) try { if (File.Exists(f)) File.Delete(f); } catch { }
        }
    }

    [CollectionDefinition("sync-serial", DisableParallelization = true)]
    public class SyncSerialCollection { }

    /// <summary>Fails the first N HTTP sends (simulating a flaky/unreachable network) then delegates,
    /// so the client's retry/backoff can be exercised deterministically.</summary>
    file sealed class FlakyHandler : DelegatingHandler
    {
        int _fail;
        public FlakyHandler(HttpMessageHandler inner, int failFirst) : base(inner) => _fail = failFirst;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (_fail-- > 0) throw new HttpRequestException("simulated transient network failure");
            return base.SendAsync(request, ct);
        }
    }
}
