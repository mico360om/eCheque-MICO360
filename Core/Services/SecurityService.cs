using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>
    /// Cross-platform database-at-rest encryption using SQLCipher, resolved PER DATABASE.
    /// A failure for one database never affects any other or the session (no global disable); worst case a
    /// single DB stays plaintext (with a .plainbak copy kept). Probe/migration connections use Pooling=false.
    /// </summary>
    public static class SecurityService
    {
        static bool _initialised;
        static string? _key;
        public static bool KeyAvailable => _key != null;

        static string KeyFile => Path.Combine(AppPaths.DataFolder, "security.key");

        public static void Init()
        {
            if (_initialised) return;
            _initialised = true;
            try { SQLitePCL.Batteries_V2.Init(); _key = LoadOrCreateKey(); }
            catch (Exception ex) { _key = null; Log($"Encryption key unavailable, using plaintext: {ex.Message}"); }
        }

        public static string ResolveConnectionString(string path)
        {
            Init();
            if (_key == null) return Plain(path);
            if (!File.Exists(path)) return Keyed(path);
            if (CanOpen(path, _key)) return Keyed(path);
            if (CanOpen(path, null))
            {
                if (TryMigrate(path)) return Keyed(path);
                Log($"Migration failed for {Path.GetFileName(path)}; using plaintext for THIS database only.");
                return Plain(path);
            }
            return Keyed(path);
        }

        static string Keyed(string path) => new SqliteConnectionStringBuilder { DataSource = path, Password = _key }.ToString();
        static string Plain(string path) => new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        static bool CanOpen(string path, string? key)
        {
            try
            {
                var b = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
                if (key != null) b.Password = key;
                using var conn = new SqliteConnection(b.ToString()); conn.Open();
                using var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                cmd.ExecuteScalar();
                return true;
            }
            catch { return false; }
        }

        static bool TryMigrate(string path)
        {
            try
            {
                Log($"Migrating plaintext database to encrypted: {Path.GetFileName(path)}");
                File.Copy(path, path + ".plainbak", true);
                var tmp = path + ".enc_tmp";
                if (File.Exists(tmp)) File.Delete(tmp);
                var b = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
                using (var conn = new SqliteConnection(b.ToString()))
                {
                    conn.Open();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText =
                        $"ATTACH DATABASE '{tmp.Replace("'", "''")}' AS encrypted KEY '{_key}';" +
                        "SELECT sqlcipher_export('encrypted');" +
                        "DETACH DATABASE encrypted;";
                    cmd.ExecuteNonQuery();
                }
                File.Delete(path); File.Move(tmp, path);
                Log($"Migration complete: {Path.GetFileName(path)}");
                return true;
            }
            catch (Exception ex) { Log($"Migration error for {Path.GetFileName(path)}: {ex.Message}"); return false; }
        }

        static string LoadOrCreateKey()
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            if (File.Exists(KeyFile)) return File.ReadAllText(KeyFile).Trim();
            var keyHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            File.WriteAllText(KeyFile, keyHex);
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                try { File.SetUnixFileMode(KeyFile, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
            return keyHex;
        }

        static void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.DataFolder);
                File.AppendAllText(Path.Combine(AppPaths.DataFolder, "security.log"), $"{DateTime.Now:o} | {message}{Environment.NewLine}");
            }
            catch { }
        }
    }

    public static class AppPaths
    {
        public static string DataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
    }
}
