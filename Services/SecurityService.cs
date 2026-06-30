using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Manages database-at-rest encryption using SQLCipher.
    ///
    /// Design goal: NEVER lock the user out of their data. If the native SQLCipher
    /// provider can't load, the key can't be read, or a migration fails, the service
    /// disables encryption for the session and the app keeps working on plaintext —
    /// exactly as it did before. A plaintext safety copy is written before any migration.
    /// </summary>
    public static class SecurityService
    {
        static bool _initialised;
        static string? _key;

        /// <summary>True when database encryption is active for this session.</summary>
        public static bool Enabled { get; private set; }

        static string KeyFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "eCheque_MICO360", "security.key");

        /// <summary>Initialises the SQLite provider and loads/creates the encryption key. Safe to call repeatedly.</summary>
        public static void Init()
        {
            if (_initialised) return;
            _initialised = true;
            try
            {
                SQLitePCL.Batteries_V2.Init();
                _key = LoadOrCreateKey();
                Enabled = !string.IsNullOrEmpty(_key);
            }
            catch (Exception ex)
            {
                Enabled = false;
                Log($"Encryption disabled (init failed): {ex.Message}");
            }
        }

        /// <summary>Permanently disables encryption for this session (used as a fallback on error).</summary>
        public static void Disable(string reason)
        {
            Enabled = false;
            Log($"Encryption disabled: {reason}");
        }

        /// <summary>Builds a connection string for a path, adding the key when encryption is active.</summary>
        public static string ConnectionString(string path)
            => Enabled && _key != null
                ? new SqliteConnectionStringBuilder { DataSource = path, Password = _key }.ToString()
                : new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        /// <summary>
        /// Ensures the database at <paramref name="path"/> is encrypted with the current key.
        /// Migrates a plaintext database in place (keeping a .plainbak copy). No-op if already
        /// encrypted, if the file doesn't exist yet, or if encryption is disabled.
        /// </summary>
        public static void EnsureEncrypted(string path)
        {
            if (!Enabled || _key == null || !File.Exists(path)) return;

            // 1) Already encrypted with our key?
            if (CanOpen(path, _key)) return;

            // 2) Is it a readable plaintext database? If so, migrate it.
            if (CanOpen(path, null))
            {
                try
                {
                    Log($"Migrating plaintext database to encrypted: {path}");
                    File.Copy(path, path + ".plainbak", true);   // safety copy

                    var tmp = path + ".enc_tmp";
                    if (File.Exists(tmp)) File.Delete(tmp);

                    using (var conn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString()))
                    {
                        conn.Open();
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText =
                            $"ATTACH DATABASE '{tmp.Replace("'", "''")}' AS encrypted KEY '{_key}';" +
                            "SELECT sqlcipher_export('encrypted');" +
                            "DETACH DATABASE encrypted;";
                        cmd.ExecuteNonQuery();
                    }
                    SqliteConnection.ClearAllPools();

                    File.Delete(path);
                    File.Move(tmp, path);
                    Log($"Migration complete: {path}");
                }
                catch (Exception ex)
                {
                    // Migration failed — fall back to plaintext so the user is never locked out.
                    Disable($"migration failed for {Path.GetFileName(path)}: {ex.Message}");
                }
            }
            // 3) Neither opened (new/empty file) — a keyed connection will create an encrypted DB.
        }

        static bool CanOpen(string path, string? key)
        {
            try
            {
                var cs = key != null
                    ? new SqliteConnectionStringBuilder { DataSource = path, Password = key }.ToString()
                    : new SqliteConnectionStringBuilder { DataSource = path }.ToString();
                using var conn = new SqliteConnection(cs);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                cmd.ExecuteScalar();
                return true;
            }
            catch { return false; }
            finally { SqliteConnection.ClearAllPools(); }
        }

        static string LoadOrCreateKey()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(KeyFile)!);
            if (File.Exists(KeyFile))
            {
                var protectedBytes = File.ReadAllBytes(KeyFile);
                var raw = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                return System.Text.Encoding.UTF8.GetString(raw);
            }
            // Generate a fresh 256-bit key, hex-encoded (safe for PRAGMA key quoting).
            var bytes = RandomNumberGenerator.GetBytes(32);
            var keyHex = Convert.ToHexString(bytes).ToLowerInvariant();
            var enc = ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(keyHex), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFile, enc);
            return keyHex;
        }

        static void Log(string message)
        {
            try
            {
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "security.log"), $"{DateTime.Now:o} | {message}{Environment.NewLine}");
            }
            catch { }
        }
    }
}
