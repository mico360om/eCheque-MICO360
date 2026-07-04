using Microsoft.Data.Sqlite;
using System.IO;
using eCheque.MICO360.Models;

namespace eCheque.MICO360.Services
{
    public static class CompanyService
    {
        private static string _cs = "";
        public static int CurrentCompanyId { get; private set; }
        public static string CurrentCompanyName { get; private set; } = "";

        public static void Initialize()
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
            Directory.CreateDirectory(folder);
            var masterDb = Path.Combine(folder, "companies.db");

            SecurityService.Init();
            _cs = SecurityService.ResolveConnectionString(masterDb);
            CreateTable();
            MigrateLegacyUsers();   // bring forward users from older per-company databases
            SeedDefault();
            MigrateSyncColumns(); // after seeding, so the default company/users also get SyncId + are marked dirty
        }

        /// <summary>Adds server-sync change-tracking columns to the master-tier tables (Companies, Users,
        /// MasterSettings). Idempotent — the backfill only fires the first time each SyncId column is created.</summary>
        private static void MigrateSyncColumns()
        {
            using var conn = GetConn();
            DatabaseService.Exec(conn, "CREATE TABLE IF NOT EXISTS SyncState(Entity TEXT PRIMARY KEY, LastServerVersion INTEGER DEFAULT 0)");
            DatabaseService.Exec(conn, "CREATE TABLE IF NOT EXISTS _SyncGuard(Id INTEGER PRIMARY KEY CHECK(Id=1), Active INTEGER DEFAULT 0)");
            DatabaseService.Exec(conn, "INSERT OR IGNORE INTO _SyncGuard(Id,Active) VALUES(1,0)");
            DatabaseService.Exec(conn, "UPDATE _SyncGuard SET Active=0 WHERE Id=1");
            DatabaseService.ApplySyncColumns(conn, "Companies",      guid: true,  createdCol: "CreatedDate");
            DatabaseService.ApplySyncColumns(conn, "Users",          guid: true,  createdCol: "CreatedDate");
            DatabaseService.ApplySyncColumns(conn, "MasterSettings", guid: false, createdCol: null);
        }

        private static SqliteConnection GetConn() { var c = new SqliteConnection(_cs); c.Open(); return c; }

        /// <summary>Connection to the master database that holds companies AND the single central user store.</summary>
        public static SqliteConnection GetMasterConnection() { var c = new SqliteConnection(_cs); c.Open(); return c; }

        /// <summary>Audit log at the master level (auth + user management, which are company-independent).</summary>
        public static void MasterAudit(string user, string action, string reference = "", string remarks = "")
        {
            try
            {
                using var conn = GetConn();
                using var cmd = new SqliteCommand("INSERT INTO AuditLogs(UserName,Action,RecordReference,Remarks,ActionDate)VALUES(@u,@a,@r,@rm,@d)", conn);
                cmd.Parameters.AddWithValue("@u", user); cmd.Parameters.AddWithValue("@a", action);
                cmd.Parameters.AddWithValue("@r", reference); cmd.Parameters.AddWithValue("@rm", remarks);
                cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private static void CreateTable()
        {
            using var conn = GetConn();
            // Companies, the single central Users store, and a master-level audit log all live here so
            // one login works across every company (no per-company user tables / no login-time selection).
            using var cmd = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS Companies(Id INTEGER PRIMARY KEY AUTOINCREMENT,Name TEXT NOT NULL,TradeName TEXT DEFAULT '',Address TEXT DEFAULT '',Phone TEXT DEFAULT '',Email TEXT DEFAULT '',Currency TEXT DEFAULT 'OMR',CreatedDate TEXT,IsActive INTEGER DEFAULT 1);
                CREATE TABLE IF NOT EXISTS Users(Id INTEGER PRIMARY KEY AUTOINCREMENT,Username TEXT NOT NULL UNIQUE,PasswordHash TEXT NOT NULL,FullName TEXT DEFAULT '',Email TEXT DEFAULT '',Role TEXT DEFAULT 'Accountant',IsActive INTEGER DEFAULT 1,CreatedDate TEXT,LastLogin TEXT,FailedLoginAttempts INTEGER DEFAULT 0,LockoutUntil TEXT);
                CREATE TABLE IF NOT EXISTS AuditLogs(Id INTEGER PRIMARY KEY AUTOINCREMENT,UserName TEXT DEFAULT '',Action TEXT DEFAULT '',RecordReference TEXT DEFAULT '',Remarks TEXT DEFAULT '',ActionDate TEXT);
                CREATE TABLE IF NOT EXISTS MasterSettings(Key TEXT PRIMARY KEY,Value TEXT DEFAULT '');
                CREATE TABLE IF NOT EXISTS BugReports(Id INTEGER PRIMARY KEY AUTOINCREMENT,CreatedDate TEXT,AppVersion TEXT,UserName TEXT,Context TEXT,Message TEXT,StackTrace TEXT);", conn);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Global settings stored in the master DB (e.g. Mailjet email config used at login, before any company opens).</summary>
        public static string GetMasterSetting(string key, string def = "")
        {
            using var conn = GetConn();
            using var cmd = new SqliteCommand("SELECT Value FROM MasterSettings WHERE Key=@k", conn);
            cmd.Parameters.AddWithValue("@k", key);
            return cmd.ExecuteScalar()?.ToString() ?? def;
        }

        public static void SetMasterSetting(string key, string value)
        {
            using var conn = GetConn();
            using var cmd = new SqliteCommand("INSERT OR REPLACE INTO MasterSettings(Key,Value)VALUES(@k,@v)", conn);
            cmd.Parameters.AddWithValue("@k", key); cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// One-time upgrade path: earlier versions stored user accounts inside each company database.
        /// If the central store has no users yet, import them from the first legacy per-company DB found,
        /// so existing customers keep their accounts (and aren't reset to the default admin) after upgrade.
        /// </summary>
        private static void MigrateLegacyUsers()
        {
            try
            {
                using var conn = GetConn();
                using (var c = new SqliteCommand("SELECT COUNT(*) FROM Users", conn))
                    if (Convert.ToInt32(c.ExecuteScalar()) > 0) return;   // central store already populated

                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
                if (!Directory.Exists(folder)) return;
                foreach (var db in Directory.GetFiles(folder, "company_*.db"))
                {
                    try
                    {
                        using var src = new SqliteConnection(SecurityService.ResolveConnectionString(db));
                        src.Open();
                        using (var chk = new SqliteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Users'", src))
                            if (Convert.ToInt32(chk.ExecuteScalar()) == 0) continue;

                        int imported = 0;
                        using var read = new SqliteCommand("SELECT Username,PasswordHash,FullName,Email,Role,IsActive,CreatedDate,FailedLoginAttempts,LockoutUntil FROM Users", src);
                        using var r = read.ExecuteReader();
                        while (r.Read())
                        {
                            using var ins = new SqliteCommand("INSERT OR IGNORE INTO Users(Username,PasswordHash,FullName,Email,Role,IsActive,CreatedDate,FailedLoginAttempts,LockoutUntil)VALUES(@u,@h,@fn,@e,@ro,@a,@cd,@fa,@lu)", conn);
                            ins.Parameters.AddWithValue("@u", r.GetString(0)); ins.Parameters.AddWithValue("@h", r.GetString(1));
                            ins.Parameters.AddWithValue("@fn", r.IsDBNull(2) ? "" : r.GetString(2)); ins.Parameters.AddWithValue("@e", r.IsDBNull(3) ? "" : r.GetString(3));
                            ins.Parameters.AddWithValue("@ro", r.GetString(4)); ins.Parameters.AddWithValue("@a", r.GetInt32(5));
                            ins.Parameters.AddWithValue("@cd", r.IsDBNull(6) ? "" : r.GetString(6)); ins.Parameters.AddWithValue("@fa", r.IsDBNull(7) ? 0 : r.GetInt32(7));
                            ins.Parameters.AddWithValue("@lu", r.IsDBNull(8) ? (object)DBNull.Value : r.GetString(8));
                            imported += ins.ExecuteNonQuery();
                        }
                        if (imported > 0) { MasterAudit("SYSTEM", "Users Migrated", Path.GetFileName(db), $"Imported {imported} user(s) into the central store"); return; }
                    }
                    catch { /* skip a db we can't read; try the next */ }
                }
            }
            catch { }
        }

        private static void SeedDefault()
        {
            using var conn = GetConn();
            using (var c = new SqliteCommand("SELECT COUNT(*) FROM Companies", conn))
                if (Convert.ToInt32(c.ExecuteScalar()) == 0)
                {
                    using var ins = new SqliteCommand("INSERT INTO Companies(Name,TradeName,Currency,CreatedDate,IsActive)VALUES('My Company LLC','','OMR',@d,1)", conn);
                    ins.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
                    ins.ExecuteNonQuery();
                }
            // Seed the default administrator into the central store (migrating from any legacy per-company admin).
            using (var c = new SqliteCommand("SELECT COUNT(*) FROM Users", conn))
                if (Convert.ToInt32(c.ExecuteScalar()) == 0)
                {
                    var h = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                    using var ins = new SqliteCommand("INSERT INTO Users(Username,PasswordHash,FullName,Role,IsActive,CreatedDate,FailedLoginAttempts)VALUES('admin',@h,'System Administrator','Admin',1,@d,0)", conn);
                    ins.Parameters.AddWithValue("@h", h); ins.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
                    ins.ExecuteNonQuery();
                }
        }

        public static List<Company> GetAll()
        {
            var list = new List<Company>();
            using var conn = GetConn();
            using var cmd = new SqliteCommand("SELECT Id,Name,TradeName,Address,Phone,Email,Currency,CreatedDate,IsActive FROM Companies WHERE IsActive=1 ORDER BY Name", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new Company {
                    Id = r.GetInt32(0), Name = r.GetString(1),
                    TradeName = r.IsDBNull(2) ? "" : r.GetString(2),
                    Address = r.IsDBNull(3) ? "" : r.GetString(3),
                    Phone = r.IsDBNull(4) ? "" : r.GetString(4),
                    Email = r.IsDBNull(5) ? "" : r.GetString(5),
                    Currency = r.IsDBNull(6) ? "OMR" : r.GetString(6),
                    CreatedDate = r.IsDBNull(7) ? "" : r.GetString(7),
                    IsActive = r.GetInt32(8) == 1
                });
            return list;
        }

        public static void Save(Company c)
        {
            using var conn = GetConn();
            if (c.Id == 0)
            {
                using var cmd = new SqliteCommand("INSERT INTO Companies(Name,TradeName,Address,Phone,Email,Currency,CreatedDate,IsActive)VALUES(@n,@t,@a,@p,@e,@cu,@d,1)", conn);
                cmd.Parameters.AddWithValue("@n", c.Name); cmd.Parameters.AddWithValue("@t", c.TradeName);
                cmd.Parameters.AddWithValue("@a", c.Address); cmd.Parameters.AddWithValue("@p", c.Phone);
                cmd.Parameters.AddWithValue("@e", c.Email); cmd.Parameters.AddWithValue("@cu", c.Currency);
                cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            else
            {
                using var cmd = new SqliteCommand("UPDATE Companies SET Name=@n,TradeName=@t,Address=@a,Phone=@p,Email=@e,Currency=@cu WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@n", c.Name); cmd.Parameters.AddWithValue("@t", c.TradeName);
                cmd.Parameters.AddWithValue("@a", c.Address); cmd.Parameters.AddWithValue("@p", c.Phone);
                cmd.Parameters.AddWithValue("@e", c.Email); cmd.Parameters.AddWithValue("@cu", c.Currency);
                cmd.Parameters.AddWithValue("@id", c.Id);
                cmd.ExecuteNonQuery();
            }
        }

        public static void Delete(int id)
        {
            using var conn = GetConn();
            using var cmd = new SqliteCommand("UPDATE Companies SET IsActive=0 WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public static string GetDbPath(int companyId)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
            return Path.Combine(folder, $"company_{companyId}.db");
        }

        /// <summary>
        /// Opens a company's data database and makes it the active one. Used at login (default company)
        /// and by the in-app company switcher. One-time-migrates a legacy DPAPI-wrapped (.enc) database
        /// if the plaintext/SQLCipher .db doesn't exist yet; SQLCipher then protects it at rest.
        /// </summary>
        public static void OpenCompany(int id, string name)
        {
            var dbPath = GetDbPath(id);
            if (!File.Exists(dbPath) && File.Exists(dbPath + ".enc"))
                DatabaseProtectionService.DecryptOnStartup(dbPath);
            DatabaseService.Initialize(dbPath);
            SelectCompany(id, name);
        }

        public static void SelectCompany(int id, string name)
        {
            CurrentCompanyId = id;
            CurrentCompanyName = name;
        }
    }
}
