using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>
    /// Cross-platform database-at-rest encryption using SQLCipher.
    ///
    /// The encryption key is stored in a key file inside the app-data folder. On macOS/Linux the
    /// file is locked to owner-only (chmod 600). Same never-lock-you-out guarantee as the Windows
    /// build: any failure disables encryption for the session and the app keeps working on plaintext.
    /// (A future enhancement can move the key into the macOS Keychain.)
    /// </summary>
    public static class SecurityService
    {
        static bool _initialised;
        static string? _key;
        public static bool Enabled { get; private set; }

        static string KeyFile => Path.Combine(AppPaths.DataFolder, "security.key");

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
            catch (Exception ex) { Enabled = false; Log($"Encryption disabled (init failed): {ex.Message}"); }
        }

        public static void Disable(string reason) { Enabled = false; Log($"Encryption disabled: {reason}"); }

        public static string ConnectionString(string path)
            => Enabled && _key != null
                ? new SqliteConnectionStringBuilder { DataSource = path, Password = _key }.ToString()
                : new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        public static void EnsureEncrypted(string path)
        {
            if (!Enabled || _key == null || !File.Exists(path)) return;
            if (CanOpen(path, _key)) return;                        // already encrypted

            if (CanOpen(path, null))                                // plaintext → migrate
            {
                try
                {
                    Log($"Migrating plaintext database to encrypted: {path}");
                    File.Copy(path, path + ".plainbak", true);
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
                catch (Exception ex) { Disable($"migration failed for {Path.GetFileName(path)}: {ex.Message}"); }
            }
        }

        static bool CanOpen(string path, string? key)
        {
            try
            {
                var cs = key != null
                    ? new SqliteConnectionStringBuilder { DataSource = path, Password = key }.ToString()
                    : new SqliteConnectionStringBuilder { DataSource = path }.ToString();
                using var conn = new SqliteConnection(cs); conn.Open();
                using var cmd = conn.CreateCommand(); cmd.CommandText = "SELECT count(*) FROM sqlite_master;";
                cmd.ExecuteScalar();
                return true;
            }
            catch { return false; }
            finally { SqliteConnection.ClearAllPools(); }
        }

        static string LoadOrCreateKey()
        {
            Directory.CreateDirectory(AppPaths.DataFolder);
            if (File.Exists(KeyFile))
                return File.ReadAllText(KeyFile).Trim();

            var keyHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            File.WriteAllText(KeyFile, keyHex);
            TryLockDown(KeyFile);
            return keyHex;
        }

        static void TryLockDown(string file)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            try { File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
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

    /// <summary>Centralized data-folder resolution (works on macOS, Linux, and Windows).</summary>
    public static class AppPaths
    {
        public static string DataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
    }
}
