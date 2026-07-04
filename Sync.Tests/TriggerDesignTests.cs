using Microsoft.Data.Sqlite;
using Xunit;

namespace eCheque.MICO360.Sync.Tests
{
    /// <summary>
    /// Validates the change-tracking TRIGGER design before it is adopted in the real migration:
    ///  - a normal write auto-stamps SyncId (if empty) + UpdatedAtUtc + Dirty=1 on INSERT and UPDATE;
    ///  - the sync engine flips a one-row guard (_SyncGuard.Active=1) inside its write transaction, which
    ///    suppresses the triggers so applying pulled changes (Dirty=0) does not get re-dirtied;
    ///  - because SQLite serializes writers, a UI write cannot interleave inside that guarded window.
    /// </summary>
    public class TriggerDesignTests
    {
        static string DDL(string t) => $@"
            CREATE TABLE IF NOT EXISTS _SyncGuard(Id INTEGER PRIMARY KEY CHECK(Id=1), Active INTEGER DEFAULT 0);
            INSERT OR IGNORE INTO _SyncGuard(Id,Active) VALUES(1,0);
            CREATE TABLE {t}(Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT,
                SyncId TEXT, UpdatedAtUtc TEXT, Deleted INTEGER DEFAULT 0, Dirty INTEGER DEFAULT 0, ServerVersion INTEGER DEFAULT 0);
            CREATE TRIGGER trg_{t}_ins AFTER INSERT ON {t} FOR EACH ROW
            WHEN (SELECT Active FROM _SyncGuard WHERE Id=1)=0
            BEGIN
              UPDATE {t} SET
                SyncId = CASE WHEN NEW.SyncId IS NULL OR NEW.SyncId='' THEN lower(hex(randomblob(16))) ELSE NEW.SyncId END,
                UpdatedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ','now'), Dirty = 1
              WHERE Id = NEW.Id;
            END;
            CREATE TRIGGER trg_{t}_upd AFTER UPDATE ON {t} FOR EACH ROW
            WHEN (SELECT Active FROM _SyncGuard WHERE Id=1)=0
            BEGIN
              UPDATE {t} SET UpdatedAtUtc = strftime('%Y-%m-%dT%H:%M:%fZ','now'), Dirty = 1
              WHERE Id = NEW.Id;
            END;";

        [Fact]
        public void Triggers_stamp_normal_writes_and_exempt_the_guarded_sync_writes()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trg_{System.Guid.NewGuid():N}.db");
            var cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
            try
            {
                using (var setup = new SqliteConnection(cs)) { setup.Open(); Exec(setup, DDL("Widgets")); }

                // --- Normal write: INSERT with no SyncId gets stamped + dirtied ---
                using (var app = new SqliteConnection(cs))
                {
                    app.Open();
                    Exec(app, "INSERT INTO Widgets(Name) VALUES('first')");
                    Assert.False(string.IsNullOrEmpty(Str(app, "SELECT SyncId FROM Widgets WHERE Name='first'")));
                    Assert.False(string.IsNullOrEmpty(Str(app, "SELECT UpdatedAtUtc FROM Widgets WHERE Name='first'")));
                    Assert.Equal(1L, Long(app, "SELECT Dirty FROM Widgets WHERE Name='first'"));

                    // A normal UPDATE that tries to clear Dirty is overridden by the trigger (still an edit).
                    Exec(app, "UPDATE Widgets SET Dirty=0 WHERE Name='first'");
                    Assert.Equal(1L, Long(app, "SELECT Dirty FROM Widgets WHERE Name='first'"));
                }

                // --- Sync engine: guard ON inside a write transaction, so its writes are exempt ---
                using (var sync = new SqliteConnection(cs))
                {
                    sync.Open();
                    Exec(sync, "BEGIN");
                    Exec(sync, "UPDATE _SyncGuard SET Active=1 WHERE Id=1");
                    Exec(sync, "UPDATE Widgets SET Dirty=0 WHERE Name='first'");          // applying a pulled change
                    Exec(sync, "INSERT INTO Widgets(Name,SyncId,Dirty) VALUES('fromserver','abc123',0)");
                    Assert.Equal(0L, Long(sync, "SELECT Dirty FROM Widgets WHERE Name='first'"));       // suppressed
                    Assert.Equal("abc123", Str(sync, "SELECT SyncId FROM Widgets WHERE Name='fromserver'"));
                    Assert.Equal(0L, Long(sync, "SELECT Dirty FROM Widgets WHERE Name='fromserver'"));
                    Exec(sync, "UPDATE _SyncGuard SET Active=0 WHERE Id=1");
                    Exec(sync, "COMMIT");
                }

                // --- Guard is back off: a normal edit dirties again ---
                using (var app2 = new SqliteConnection(cs))
                {
                    app2.Open();
                    Exec(app2, "UPDATE Widgets SET Name='renamed' WHERE Name='fromserver'");
                    Assert.Equal(1L, Long(app2, "SELECT Dirty FROM Widgets WHERE Name='renamed'"));
                    Assert.Equal(0L, Long(app2, "SELECT Active FROM _SyncGuard WHERE Id=1")); // guard left off
                }
            }
            finally { SqliteConnection.ClearAllPools(); try { System.IO.File.Delete(path); } catch { } }
        }

        static void Exec(SqliteConnection c, string sql) { using var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.ExecuteNonQuery(); }
        static string? Str(SqliteConnection c, string sql) { using var cmd = c.CreateCommand(); cmd.CommandText = sql; var v = cmd.ExecuteScalar(); return v == System.DBNull.Value ? null : (string?)v; }
        static long Long(SqliteConnection c, string sql) { using var cmd = c.CreateCommand(); cmd.CommandText = sql; return System.Convert.ToInt64(cmd.ExecuteScalar()); }
    }
}
