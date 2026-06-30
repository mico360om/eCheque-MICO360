using System.IO;
using System.Security.Cryptography;

namespace eCheque.MICO360.Services
{
    public static class DatabaseProtectionService
    {
        private static readonly byte[] Entropy = "eCheque_MICO360_v2_Secure"u8.ToArray();

        public static void DecryptOnStartup(string dbPath)
        {
            var encPath = dbPath + ".enc";
            if (!File.Exists(encPath)) return;
            try
            {
                var encrypted = File.ReadAllBytes(encPath);
                var decrypted = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(dbPath, decrypted);
            }
            catch { /* Different user or corrupt file — fresh start */ }
        }

        public static void EncryptOnExit(string dbPath)
        {
            if (!File.Exists(dbPath)) return;
            var encPath = dbPath + ".enc";
            try
            {
                var data = File.ReadAllBytes(dbPath);
                var encrypted = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(encPath, encrypted);
            }
            catch { /* Best-effort encryption */ }
        }
    }
}
