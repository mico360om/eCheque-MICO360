using System.IO;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Database-at-rest encryption using SQLCipher, resolved PER DATABASE.
    ///
    /// <see cref="ResolveConnectionString"/> decides, for each individual database file, whether to
    /// open it keyed (encrypted) or plain — migrating a plaintext file to SQLCipher when possible.
    /// A failure for one database never affects any other database or the session (no global disable),
    /// and the app is never locked out: worst case a single DB stays plaintext (with a .plainbak copy kept).
    /// Probe/migration connections use Pooling=false so they never disturb the app's pooled connections.
    /// </summary>
    public static class SecurityService
    {
        static bool _initialised;
        static string? _key;

        public static bool KeyAvailable => _key != null;

        static string KeyFile => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360", "security.key");

        public static void Init()
        {
            if (_initialised) return;
            _initialised = true;
            try
            {
                SQLitePCL.Batteries_V2.Init();
                _key = LoadOrCreateKey();
            }
            catch (Exception ex) { _key = null; Log($"Encryption key unavailable, using plaintext: {ex.Message}"); }
        }

        /// <summary>Returns the connection string to use for one specific database, migrating it to SQLCipher if needed.</summary>
        public static string ResolveConnectionString(string path)
        {
            Init();
            if (_key == null) return Plain(path);            // no key available → plaintext everywhere
            if (!File.Exists(path)) return Keyed(path);      // new file → created encrypted
            if (CanOpen(path, _key)) return Keyed(path);     // already encrypted with our key
            if (CanOpen(path, null))                         // readable plaintext → migrate this file
            {
                if (TryMigrate(path)) return Keyed(path);
                Log($"Migration failed for {Path.GetFileName(path)}; using plaintext for THIS database only.");
                return Plain(path);                          // per-DB fallback (does not affect other DBs)
            }
            return Keyed(path);                              // not plaintext and not our key → treat as encrypted/new
        }

        static string Keyed(string path) => new SqliteConnectionStringBuilder { DataSource = path, Password = _key }.ToString();
        static string Plain(string path) => new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        static bool CanOpen(string path, string? key)
        {
            try
            {
                var b = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false };
                if (key != null) b.Password = key;
                using var conn = new SqliteConnection(b.ToString());
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
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
                File.Delete(path);
                File.Move(tmp, path);

                // Verify the encrypted DB opens with our key, THEN destroy the plaintext backup. Leaving a
                // .plainbak next to the encrypted file would defeat at-rest encryption for anyone with disk
                // access. Only if verification fails do we keep the backup (so data is never lost).
                if (CanOpen(path, _key))
                {
                    ShredAndDelete(path + ".plainbak");
                    Log($"Migration complete (plaintext backup destroyed): {Path.GetFileName(path)}");
                }
                else
                {
                    Log($"Migration produced an unreadable encrypted DB for {Path.GetFileName(path)}; keeping .plainbak for recovery.");
                    return false;
                }
                return true;
            }
            catch (Exception ex) { Log($"Migration error for {Path.GetFileName(path)}: {ex.Message}"); return false; }
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
            var keyHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var enc = ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(keyHex), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(KeyFile, enc);
            return keyHex;
        }

        /// <summary>Overwrites a file with random bytes before deleting, so the plaintext is not trivially
        /// recoverable. Best-effort (SSD wear-levelling limits guarantees) — deletion still happens if overwrite fails.</summary>
        static void ShredAndDelete(string path)
        {
            try
            {
                if (!File.Exists(path)) return;
                try
                {
                    var len = new FileInfo(path).Length;
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        var buf = new byte[81920];
                        long written = 0;
                        while (written < len)
                        {
                            RandomNumberGenerator.Fill(buf);
                            int n = (int)Math.Min(buf.Length, len - written);
                            fs.Write(buf, 0, n);
                            written += n;
                        }
                        fs.Flush(true);
                    }
                }
                catch { /* overwrite failed — still delete below */ }
                File.Delete(path);
            }
            catch (Exception ex) { Log($"Could not remove plaintext backup {Path.GetFileName(path)}: {ex.Message}"); }
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
