using Microsoft.Data.Sqlite;
using System.IO;

namespace eCheque.MICO360.Services
{
    public static class DatabaseService
    {
        private static string _cs = "";
        public static string DbPath { get; private set; } = "";

        public static void Initialize(string? dbPath = null)
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
            Directory.CreateDirectory(folder);
            DbPath = dbPath ?? Path.Combine(folder, "echeque.db");

            SecurityService.Init();
            // Per-database resolution: encrypts/opens THIS db, never affecting other databases.
            _cs = SecurityService.ResolveConnectionString(DbPath);
            CreateTables();
            MigrateSchema();
            MigrateSyncColumns();
            SeedData();
        }

        /// <summary>
        /// Adds change-tracking columns used by server sync to the per-company tables and backfills identities
        /// exactly once. Runs every startup but is idempotent: the GUID/Dirty backfill only fires the first time
        /// each SyncId column is created, so relaunching never re-marks rows dirty. (Mirror of the Core copy.)
        /// </summary>
        private static void MigrateSyncColumns()
        {
            using var conn = GetConnection();
            using (var st = new SqliteCommand(
                "CREATE TABLE IF NOT EXISTS SyncState(Entity TEXT PRIMARY KEY, LastServerVersion INTEGER DEFAULT 0)", conn))
                st.ExecuteNonQuery();

            ApplySyncColumns(conn, "ChequeProfiles", guid: true,  createdCol: "CreatedDate");
            ApplySyncColumns(conn, "ChequeRecords",  guid: true,  createdCol: "CreatedDate", profileFk: true);
            ApplySyncColumns(conn, "Banks",          guid: true,  createdCol: null);
            ApplySyncColumns(conn, "Payees",         guid: false, createdCol: "LastUsed");
            ApplySyncColumns(conn, "AppSettings",    guid: false, createdCol: null);
        }

        internal static void ApplySyncColumns(SqliteConnection conn, string table, bool guid, string? createdCol, bool profileFk = false)
        {
            bool fresh = TryExec(conn, $"ALTER TABLE {table} ADD COLUMN SyncId TEXT");
            TryExec(conn, $"ALTER TABLE {table} ADD COLUMN UpdatedAtUtc TEXT");
            TryExec(conn, $"ALTER TABLE {table} ADD COLUMN Deleted INTEGER DEFAULT 0");
            TryExec(conn, $"ALTER TABLE {table} ADD COLUMN Dirty INTEGER DEFAULT 0");
            TryExec(conn, $"ALTER TABLE {table} ADD COLUMN ServerVersion INTEGER DEFAULT 0");
            if (profileFk) TryExec(conn, $"ALTER TABLE {table} ADD COLUMN ProfileSyncId TEXT");

            if (!fresh) return;

            if (guid)
                Exec(conn, $"UPDATE {table} SET SyncId = lower(hex(randomblob(16))) WHERE SyncId IS NULL OR SyncId = ''");
            string createdExpr = createdCol != null ? $"NULLIF({createdCol},'')" : "NULL";
            Exec(conn, $"UPDATE {table} SET UpdatedAtUtc = COALESCE({createdExpr}, strftime('%Y-%m-%dT%H:%M:%fZ','now')) WHERE UpdatedAtUtc IS NULL OR UpdatedAtUtc = ''");
            Exec(conn, $"UPDATE {table} SET Dirty = 1");
            if (profileFk)
                Exec(conn, $"UPDATE {table} SET ProfileSyncId = (SELECT p.SyncId FROM ChequeProfiles p WHERE p.Id = {table}.ProfileId) WHERE (ProfileSyncId IS NULL OR ProfileSyncId='') AND ProfileId > 0");
            if (guid) TryExec(conn, $"CREATE UNIQUE INDEX IF NOT EXISTS IX_{table}_SyncId ON {table}(SyncId)");
        }

        internal static bool TryExec(SqliteConnection conn, string sql)
        {
            try { using var c = new SqliteCommand(sql, conn); c.ExecuteNonQuery(); return true; }
            catch { return false; }
        }

        internal static void Exec(SqliteConnection conn, string sql)
        {
            try { using var c = new SqliteCommand(sql, conn); c.ExecuteNonQuery(); } catch { }
        }

        public static SqliteConnection GetConnection() { var c = new SqliteConnection(_cs); c.Open(); return c; }

        private static void CreateTables()
        {
            using var conn = GetConnection();
            var sql = @"
                CREATE TABLE IF NOT EXISTS Users(Id INTEGER PRIMARY KEY AUTOINCREMENT,Username TEXT NOT NULL UNIQUE,PasswordHash TEXT NOT NULL,FullName TEXT DEFAULT '',Email TEXT DEFAULT '',Role TEXT DEFAULT 'Accountant',IsActive INTEGER DEFAULT 1,CreatedDate TEXT,LastLogin TEXT,FailedLoginAttempts INTEGER DEFAULT 0,LockoutUntil TEXT);
                CREATE TABLE IF NOT EXISTS ChequeProfiles(Id INTEGER PRIMARY KEY AUTOINCREMENT,Name TEXT NOT NULL,BankName TEXT DEFAULT '',AccountName TEXT DEFAULT '',AccountNumber TEXT DEFAULT '',ChequeWidth REAL DEFAULT 190,ChequeHeight REAL DEFAULT 85,DateX REAL DEFAULT 140,DateY REAL DEFAULT 18,PayeeX REAL DEFAULT 25,PayeeY REAL DEFAULT 35,AmountNumX REAL DEFAULT 140,AmountNumY REAL DEFAULT 35,AmountWordsX REAL DEFAULT 25,AmountWordsY REAL DEFAULT 50,ChequeNumX REAL DEFAULT 25,ChequeNumY REAL DEFAULT 65,FontFamily TEXT DEFAULT 'Arial',FontSize REAL DEFAULT 11,IsBold INTEGER DEFAULT 0,PrintOffsetX REAL DEFAULT 0,PrintOffsetY REAL DEFAULT 0,PaperSize TEXT DEFAULT 'A4',IsActive INTEGER DEFAULT 1,CreatedDate TEXT,CreatedBy TEXT DEFAULT '',LastChequeNumber INTEGER DEFAULT 0);
                CREATE TABLE IF NOT EXISTS ChequeRecords(Id INTEGER PRIMARY KEY AUTOINCREMENT,ChequeNumber TEXT NOT NULL,ChequeDate TEXT NOT NULL,PayeeName TEXT NOT NULL,Amount REAL DEFAULT 0,AmountInWords TEXT DEFAULT '',BankName TEXT DEFAULT '',AccountName TEXT DEFAULT '',AccountNumber TEXT DEFAULT '',ProfileId INTEGER DEFAULT 0,ProfileName TEXT DEFAULT '',Currency TEXT DEFAULT 'OMR',Remarks TEXT DEFAULT '',ReferenceNumber TEXT DEFAULT '',InvoiceNumber TEXT DEFAULT '',VoucherNumber TEXT DEFAULT '',PreparedBy TEXT DEFAULT '',ApprovedBy TEXT DEFAULT '',Department TEXT DEFAULT '',PaymentCategory TEXT DEFAULT '',Status TEXT DEFAULT 'Draft',CreatedBy TEXT DEFAULT '',CreatedDate TEXT,PrintedDate TEXT,PrintCount INTEGER DEFAULT 0,PdfFilePath TEXT DEFAULT '',CancellationReason TEXT DEFAULT '');
                CREATE TABLE IF NOT EXISTS AuditLogs(Id INTEGER PRIMARY KEY AUTOINCREMENT,UserName TEXT DEFAULT '',Action TEXT DEFAULT '',RecordReference TEXT DEFAULT '',Remarks TEXT DEFAULT '',ActionDate TEXT);
                CREATE TABLE IF NOT EXISTS PrintHistory(Id INTEGER PRIMARY KEY AUTOINCREMENT,ChequeId INTEGER,ChequeNumber TEXT DEFAULT '',PrintedBy TEXT DEFAULT '',PrintedDate TEXT,Reason TEXT DEFAULT '',IsReprint INTEGER DEFAULT 0);
                CREATE TABLE IF NOT EXISTS AppSettings(Key TEXT PRIMARY KEY,Value TEXT DEFAULT '');
                CREATE TABLE IF NOT EXISTS Banks(Id INTEGER PRIMARY KEY AUTOINCREMENT,Name TEXT NOT NULL,IsActive INTEGER DEFAULT 1);
                CREATE TABLE IF NOT EXISTS Payees(Name TEXT PRIMARY KEY,LastUsed TEXT);";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }

        private static void MigrateSchema()
        {
            // Safely add columns that may not exist in older database files
            var migrations = new[]
            {
                "ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER DEFAULT 0",
                "ALTER TABLE Users ADD COLUMN LockoutUntil TEXT",
                "ALTER TABLE ChequeProfiles ADD COLUMN LastChequeNumber INTEGER DEFAULT 0",
                // Cheque lifecycle / reconciliation + PDC tracking
                "ALTER TABLE ChequeRecords ADD COLUMN PresentedDate TEXT",
                "ALTER TABLE ChequeRecords ADD COLUMN ClearedDate TEXT",
                "ALTER TABLE ChequeRecords ADD COLUMN BounceReason TEXT DEFAULT ''",
                "ALTER TABLE ChequeProfiles ADD COLUMN BackgroundImage TEXT DEFAULT ''",
                "ALTER TABLE ChequeProfiles ADD COLUMN FieldsJson TEXT DEFAULT ''"
            };
            using var conn = GetConnection();
            foreach (var sql in migrations)
                try { using var cmd = new SqliteCommand(sql, conn); cmd.ExecuteNonQuery(); } catch { }
        }

        private static void SeedData()
        {
            using var conn = GetConnection();
            // NOTE: user accounts live in the central master DB now, so we do NOT seed an admin into
            // per-company databases (that would ship default credentials in every company file).
            // Backfill the saved-payees list from any existing cheques (idempotent).
            try { using var pf = new SqliteCommand("INSERT OR IGNORE INTO Payees(Name,LastUsed) SELECT PayeeName, MAX(CreatedDate) FROM ChequeRecords WHERE PayeeName!='' GROUP BY PayeeName", conn); pf.ExecuteNonQuery(); } catch { }
            using (var c = new SqliteCommand("SELECT COUNT(*) FROM Banks", conn))
                if (Convert.ToInt32(c.ExecuteScalar()) == 0)
                    foreach (var b in new[]{"Bank Muscat","National Bank of Oman","Sohar International","Bank Dhofar","HSBC Oman","Oman Arab Bank","First Abu Dhabi Bank"})
                    { using var ins = new SqliteCommand("INSERT INTO Banks(Name,IsActive)VALUES(@n,1)",conn); ins.Parameters.AddWithValue("@n",b); ins.ExecuteNonQuery(); }
            using (var c = new SqliteCommand("SELECT COUNT(*) FROM ChequeProfiles", conn))
                if (Convert.ToInt32(c.ExecuteScalar()) == 0)
                    foreach (var p in new[]{("Bank Muscat - Standard","Bank Muscat"),("NBO - Standard","National Bank of Oman"),("Sohar International - Standard","Sohar International")})
                    { using var ins = new SqliteCommand("INSERT INTO ChequeProfiles(Name,BankName,ChequeWidth,ChequeHeight,DateX,DateY,PayeeX,PayeeY,AmountNumX,AmountNumY,AmountWordsX,AmountWordsY,ChequeNumX,ChequeNumY,FontFamily,FontSize,IsBold,PrintOffsetX,PrintOffsetY,PaperSize,IsActive,CreatedDate,CreatedBy,LastChequeNumber)VALUES(@n,@b,190,85,140,18,25,35,140,35,25,50,25,65,'Arial',11,0,0,0,'A4',1,@d,'System',0)",conn); ins.Parameters.AddWithValue("@n",p.Item1); ins.Parameters.AddWithValue("@b",p.Item2); ins.Parameters.AddWithValue("@d",DateTime.Now.ToString("o")); ins.ExecuteNonQuery(); }
            using (var c = new SqliteCommand("SELECT COUNT(*) FROM AppSettings", conn))
                if (Convert.ToInt32(c.ExecuteScalar()) == 0)
                {
                    var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    foreach (var kv in new Dictionary<string,string>{["CompanyName"]="My Company LLC",["DefaultCurrency"]="OMR",["DateFormat"]="dd/MM/yyyy",["AmountCaseFormat"]="UPPERCASE",["WarnOnReprint"]="true",["SessionTimeoutMinutes"]="10",["PdfSavePath"]=Path.Combine(docs,"eCheque MICO360","PDFs"),["BackupPath"]=Path.Combine(docs,"eCheque MICO360","Backups"),["AmountCurrencyWording"]="Omani Rials",["AmountBaisaWording"]="Baisa",["AmountIncludeBaisa"]="true",["AmountAddOnly"]="true"})
                    { using var ins = new SqliteCommand("INSERT OR IGNORE INTO AppSettings(Key,Value)VALUES(@k,@v)",conn); ins.Parameters.AddWithValue("@k",kv.Key); ins.Parameters.AddWithValue("@v",kv.Value); ins.ExecuteNonQuery(); }
                }

            // Legal content seed — runs for new AND existing databases (INSERT OR IGNORE keeps any admin edits).
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            foreach (var kv in new Dictionary<string,string>{
                ["Legal_Terms_Content"]   = Helpers.AppInfo.DefaultTerms,
                ["Legal_Terms_Updated"]   = today,
                ["Legal_Privacy_Content"] = Helpers.AppInfo.DefaultPrivacy,
                ["Legal_Privacy_Updated"] = today,
                ["Legal_About_Intro"]     = Helpers.AppInfo.CompanyIntro,
                ["Legal_About_Updated"]   = today })
            { using var ins = new SqliteCommand("INSERT OR IGNORE INTO AppSettings(Key,Value)VALUES(@k,@v)",conn); ins.Parameters.AddWithValue("@k",kv.Key); ins.Parameters.AddWithValue("@v",kv.Value); ins.ExecuteNonQuery(); }
        }

        public static void LogAudit(string user, string action, string reference = "", string remarks = "")
        {
            try
            {
                using var conn = GetConnection();
                using var cmd = new SqliteCommand("INSERT INTO AuditLogs(UserName,Action,RecordReference,Remarks,ActionDate)VALUES(@u,@a,@r,@rm,@d)",conn);
                cmd.Parameters.AddWithValue("@u",user); cmd.Parameters.AddWithValue("@a",action); cmd.Parameters.AddWithValue("@r",reference); cmd.Parameters.AddWithValue("@rm",remarks); cmd.Parameters.AddWithValue("@d",DateTime.Now.ToString("o"));
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Fallback: write to log file so audit events are never silently lost
                try
                {
                    var logFile = Path.Combine(Path.GetDirectoryName(DbPath) ?? "", "audit_fallback.log");
                    File.AppendAllText(logFile, $"{DateTime.Now:o}|{user}|{action}|{reference}|{remarks}|ERROR:{ex.Message}{Environment.NewLine}");
                }
                catch { }
            }
        }

        public static string GetSetting(string key, string def = "") { using var conn = GetConnection(); using var cmd = new SqliteCommand("SELECT Value FROM AppSettings WHERE Key=@k",conn); cmd.Parameters.AddWithValue("@k",key); return cmd.ExecuteScalar()?.ToString() ?? def; }
        public static void SaveSetting(string key, string value) { using var conn = GetConnection(); using var cmd = new SqliteCommand("INSERT OR REPLACE INTO AppSettings(Key,Value)VALUES(@k,@v)",conn); cmd.Parameters.AddWithValue("@k",key); cmd.Parameters.AddWithValue("@v",value); cmd.ExecuteNonQuery(); }
    }
}
