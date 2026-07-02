using Microsoft.Data.Sqlite;
using System.IO;

namespace eCheque.MICO360.Core.Services
{
    public static class DatabaseService
    {
        private static string _cs = "";
        public static string DbPath { get; private set; } = "";

        public static void Initialize(string? dbPath = null)
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            DbPath = dbPath ?? Path.Combine(AppPaths.DataFolder, "echeque.db");

            SecurityService.Init();
            _cs = SecurityService.ResolveConnectionString(DbPath);
            CreateTables();
            MigrateSchema();
            SeedData();
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
            var migrations = new[]
            {
                "ALTER TABLE Users ADD COLUMN FailedLoginAttempts INTEGER DEFAULT 0",
                "ALTER TABLE Users ADD COLUMN LockoutUntil TEXT",
                "ALTER TABLE ChequeProfiles ADD COLUMN LastChequeNumber INTEGER DEFAULT 0",
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
            // User accounts live in the central master DB; do not seed an admin into per-company databases.
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
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            foreach (var kv in new Dictionary<string,string>{
                ["Legal_Terms_Content"]   = AppInfo.DefaultTerms,
                ["Legal_Terms_Updated"]   = today,
                ["Legal_Privacy_Content"] = AppInfo.DefaultPrivacy,
                ["Legal_Privacy_Updated"] = today,
                ["Legal_About_Intro"]     = AppInfo.CompanyIntro,
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
                try
                {
                    var logFile = Path.Combine(AppPaths.DataFolder, "audit_fallback.log");
                    File.AppendAllText(logFile, $"{DateTime.Now:o}|{user}|{action}|{reference}|{remarks}|ERROR:{ex.Message}{Environment.NewLine}");
                }
                catch { }
            }
        }

        public static string GetSetting(string key, string def = "") { using var conn = GetConnection(); using var cmd = new SqliteCommand("SELECT Value FROM AppSettings WHERE Key=@k",conn); cmd.Parameters.AddWithValue("@k",key); return cmd.ExecuteScalar()?.ToString() ?? def; }
        public static void SaveSetting(string key, string value) { using var conn = GetConnection(); using var cmd = new SqliteCommand("INSERT OR REPLACE INTO AppSettings(Key,Value)VALUES(@k,@v)",conn); cmd.Parameters.AddWithValue("@k",key); cmd.Parameters.AddWithValue("@v",value); cmd.ExecuteNonQuery(); }
    }
}
