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
            _cs = $"Data Source={masterDb}";
            CreateTable();
            SeedDefault();
        }

        private static SqliteConnection GetConn() { var c = new SqliteConnection(_cs); c.Open(); return c; }

        private static void CreateTable()
        {
            using var conn = GetConn();
            using var cmd = new SqliteCommand(@"
                CREATE TABLE IF NOT EXISTS Companies(
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    TradeName TEXT DEFAULT '',
                    Address TEXT DEFAULT '',
                    Phone TEXT DEFAULT '',
                    Email TEXT DEFAULT '',
                    Currency TEXT DEFAULT 'OMR',
                    CreatedDate TEXT,
                    IsActive INTEGER DEFAULT 1
                )", conn);
            cmd.ExecuteNonQuery();
        }

        private static void SeedDefault()
        {
            using var conn = GetConn();
            using var c = new SqliteCommand("SELECT COUNT(*) FROM Companies", conn);
            if (Convert.ToInt32(c.ExecuteScalar()) == 0)
            {
                using var ins = new SqliteCommand("INSERT INTO Companies(Name,TradeName,Currency,CreatedDate,IsActive)VALUES('My Company LLC','','OMR',@d,1)", conn);
                ins.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
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

        public static void SelectCompany(int id, string name)
        {
            CurrentCompanyId = id;
            CurrentCompanyName = name;
        }
    }
}
