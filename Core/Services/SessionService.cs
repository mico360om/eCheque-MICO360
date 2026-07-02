using System.IO;
using System.Text.Json;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>
    /// "Remember me" sign-in. When enabled, the signed-in user id is persisted locally so the app
    /// auto-signs-in on the next launch — but ONLY while the app has been used within the last 30 days.
    /// After 30 days of no use the remembered session expires and a fresh login is required. No password
    /// is stored; a random token adds minor tamper resistance. Explicit sign-out clears it.
    /// </summary>
    public static class SessionService
    {
        public const int ExpiryDays = 30;

        static string Folder
        {
            get
            {
                var f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
                Directory.CreateDirectory(f);
                return f;
            }
        }
        static string FilePath => Path.Combine(Folder, "session.json");

        class Data { public int UserId { get; set; } public string Token { get; set; } = ""; public DateTime LastActivity { get; set; } }

        /// <summary>Persist a remembered session for this user (called when "Remember me" is ticked at login).</summary>
        public static void Remember(int userId)
        {
            try { File.WriteAllText(FilePath, JsonSerializer.Serialize(new Data { UserId = userId, Token = Guid.NewGuid().ToString("N"), LastActivity = DateTime.Now })); }
            catch { }
        }

        /// <summary>Mark the app as used now, resetting the 30-day inactivity window.</summary>
        public static void Touch()
        {
            try { var d = Read(); if (d != null) { d.LastActivity = DateTime.Now; File.WriteAllText(FilePath, JsonSerializer.Serialize(d)); } }
            catch { }
        }

        /// <summary>Forget the remembered session (explicit sign-out, or expiry).</summary>
        public static void Clear()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        }

        static Data? Read()
        {
            try { return File.Exists(FilePath) ? JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) : null; }
            catch { return null; }
        }

        /// <summary>The remembered user id if a valid session exists and the app was used within the last 30 days;
        /// otherwise null (an expired session is cleared).</summary>
        public static int? RememberedUserId()
        {
            var d = Read();
            if (d == null) return null;
            if ((DateTime.Now - d.LastActivity).TotalDays > ExpiryDays) { Clear(); return null; }
            return d.UserId;
        }
    }
}
