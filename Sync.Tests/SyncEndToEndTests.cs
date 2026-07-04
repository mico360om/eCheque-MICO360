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
        const string OrgKey = "IT-ORG-KEY";
        const int Company = 5;
        static readonly SyncEntityDef[] Entities =
        {
            new() { Name = SyncEntities.ChequeProfile, Table = "ChequeProfiles" },
            new() { Name = SyncEntities.Payee, Table = "Payees", Guid = false, NaturalKey = "Name" },
        };

        readonly string _serverDb;
        readonly WebApplicationFactory<Program> _factory;
        readonly HttpClient _http;
        readonly string _tokenA, _tokenB;
        readonly List<string> _tempFiles = new();

        public SyncEndToEndTests()
        {
            _serverDb = NewTempFile("server");
            Environment.SetEnvironmentVariable("ECHEQUE_ORG_KEY", OrgKey);
            Environment.SetEnvironmentVariable("ECHEQUE_SERVER_DB", _serverDb);
            _factory = new WebApplicationFactory<Program>();
            _http = _factory.CreateClient();
            _tokenA = Register("PC-A", "MID-A");
            _tokenB = Register("PC-B", "MID-B");
        }

        string Register(string name, string mid)
        {
            var r = SyncClient.RegisterAsync(_http, "", new RegisterRequest { OrgKey = OrgKey, DeviceName = name, MachineId = mid }).Result;
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

        // ---------- client-db harness (plain SQLite; the engine is encryption-agnostic) ----------

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
}
