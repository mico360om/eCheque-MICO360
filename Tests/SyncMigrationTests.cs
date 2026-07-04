using System;
using System.IO;
using eCheque.MICO360.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>
    /// Exercises the REAL encrypted (SQLCipher) client migration — DatabaseService.MigrateSyncColumns — against
    /// a fresh per-company database, proving the sync identity/change-tracking columns are added and backfilled,
    /// and that re-launching does not re-dirty rows.
    /// </summary>
    [Collection("db-serial")]
    public class SyncMigrationTests
    {
        [Fact]
        public void Migration_adds_sync_columns_and_backfills_unique_identities()
        {
            var dir = Path.Combine(Path.GetTempPath(), "echeque_mig_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var db = Path.Combine(dir, "company_test.db");

            DatabaseService.Initialize(db); // creates tables, migrates sync columns, seeds default data

            using (var conn = DatabaseService.GetConnection())
            {
                Assert.True(TableExists(conn, "SyncState"));

                int total       = Count(conn, "SELECT COUNT(*) FROM ChequeProfiles");
                int withId      = Count(conn, "SELECT COUNT(*) FROM ChequeProfiles WHERE SyncId IS NOT NULL AND SyncId<>''");
                int distinctId  = Count(conn, "SELECT COUNT(DISTINCT SyncId) FROM ChequeProfiles");
                int withUpdated = Count(conn, "SELECT COUNT(*) FROM ChequeProfiles WHERE UpdatedAtUtc IS NOT NULL AND UpdatedAtUtc<>''");
                int dirty       = Count(conn, "SELECT COUNT(*) FROM ChequeProfiles WHERE Dirty=1");

                Assert.True(total > 0, "seed should create default profiles");
                Assert.Equal(total, withId);       // every row got a SyncId
                Assert.Equal(total, distinctId);   // all unique — no collisions
                Assert.Equal(total, withUpdated);  // every row got an UpdatedAtUtc
                Assert.Equal(total, dirty);        // seeded rows marked for first upload

                // Banks also carry the columns (GUID identity).
                Assert.Equal(Count(conn, "SELECT COUNT(*) FROM Banks"),
                             Count(conn, "SELECT COUNT(*) FROM Banks WHERE SyncId IS NOT NULL AND SyncId<>''"));

                using var clear = conn.CreateCommand();
                clear.CommandText = "UPDATE ChequeProfiles SET Dirty=0";
                clear.ExecuteNonQuery();
            }

            // Second launch must NOT re-run the backfill (idempotent) — the cleared rows stay clean.
            DatabaseService.Initialize(db);
            using (var conn2 = DatabaseService.GetConnection())
                Assert.Equal(0, Count(conn2, "SELECT COUNT(*) FROM ChequeProfiles WHERE Dirty=1"));

            try { Directory.Delete(dir, true); } catch { }
        }

        static int Count(SqliteConnection c, string sql)
        { using var cmd = c.CreateCommand(); cmd.CommandText = sql; return Convert.ToInt32(cmd.ExecuteScalar()); }

        static bool TableExists(SqliteConnection c, string name)
        { using var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@n"; cmd.Parameters.AddWithValue("@n", name); return Convert.ToInt32(cmd.ExecuteScalar()) > 0; }
    }

    [CollectionDefinition("db-serial", DisableParallelization = true)]
    public class DbSerialCollection { }
}
