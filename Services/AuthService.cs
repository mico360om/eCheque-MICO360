using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using eCheque.MICO360.Models;

namespace eCheque.MICO360.Services
{
    public static class AuthService
    {
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 5;
        private const int OtpValidMinutes = 5;
        // In-memory one-time-code store, keyed by lower-cased email.
        private static readonly Dictionary<string, (string code, DateTime expiry, int attempts)> _otps = new();

        public static User? CurrentUser { get; private set; }
        public static bool IsAdmin => CurrentUser?.Role == "Admin";
        public static bool CanEdit => CurrentUser?.Role is "Admin" or "Accountant";

        // Returns null on success, error message string on failure
        public static string? Login(string username, string password)
        {
            // --- Step 1: Read all user data, then close the reader BEFORE any writes ---
            // SQLite blocks when executing UPDATE on the same connection while a DataReader is open.
            int userId = 0; string passwordHash = ""; string dbUsername = "";
            string fullName = ""; string email = ""; string role = "";
            int failedAttempts = 0; string? lockoutStr = null;
            bool found = false;

            using (var conn = CompanyService.GetMasterConnection())
            using (var cmd = new SqliteCommand(
                "SELECT Id,Username,PasswordHash,FullName,Email,Role,FailedLoginAttempts,LockoutUntil FROM Users WHERE Username=@u AND IsActive=1", conn))
            {
                cmd.Parameters.AddWithValue("@u", username.Trim());
                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    found        = true;
                    userId       = r.GetInt32(0);
                    dbUsername   = r.GetString(1);
                    passwordHash = r.GetString(2);
                    fullName     = r.IsDBNull(3) ? "" : r.GetString(3);
                    email        = r.IsDBNull(4) ? "" : r.GetString(4);
                    role         = r.GetString(5);
                    failedAttempts = r.IsDBNull(6) ? 0 : r.GetInt32(6);
                    lockoutStr   = r.IsDBNull(7) ? null : r.GetString(7);
                }
            } // connection + reader closed here — safe to write below

            if (!found)
            {
                CompanyService.MasterAudit(username, "Login Failed", "", "User not found");
                return "Invalid username or password.";
            }

            // --- Step 2: Check lockout (no DB needed) ---
            if (lockoutStr != null && DateTime.TryParse(lockoutStr, out var lockUntil) && lockUntil > DateTime.Now)
            {
                var remaining = (int)Math.Ceiling((lockUntil - DateTime.Now).TotalMinutes);
                return $"Account locked. Try again in {remaining} minute(s).";
            }

            // --- Step 3: Verify password (CPU-bound, reader already closed) ---
            if (!BCrypt.Net.BCrypt.Verify(password, passwordHash))
            {
                failedAttempts++;
                using var wconn = CompanyService.GetMasterConnection();
                if (failedAttempts >= MaxFailedAttempts)
                {
                    var newLockUntil = DateTime.Now.AddMinutes(LockoutMinutes);
                    using var lk = new SqliteCommand(
                        "UPDATE Users SET FailedLoginAttempts=@fa,LockoutUntil=@lu WHERE Id=@id", wconn);
                    lk.Parameters.AddWithValue("@fa", failedAttempts);
                    lk.Parameters.AddWithValue("@lu", newLockUntil.ToString("o"));
                    lk.Parameters.AddWithValue("@id", userId);
                    lk.ExecuteNonQuery();
                    CompanyService.MasterAudit(username, "Account Locked", "", $"After {failedAttempts} failed attempts");
                    return $"Account locked for {LockoutMinutes} minutes after {MaxFailedAttempts} failed attempts.";
                }
                else
                {
                    using var fa = new SqliteCommand("UPDATE Users SET FailedLoginAttempts=@fa WHERE Id=@id", wconn);
                    fa.Parameters.AddWithValue("@fa", failedAttempts);
                    fa.Parameters.AddWithValue("@id", userId);
                    fa.ExecuteNonQuery();
                    int remaining = MaxFailedAttempts - failedAttempts;
                    CompanyService.MasterAudit(username, "Login Failed", "", $"Attempt {failedAttempts}/{MaxFailedAttempts}");
                    return $"Invalid password. {remaining} attempt(s) remaining before lockout.";
                }
            }

            // --- Step 4: Success — set session, reset lockout counters ---
            CurrentUser = new User { Id = userId, Username = dbUsername, FullName = fullName, Email = email, Role = role };
            using var uconn = CompanyService.GetMasterConnection();
            using var upd = new SqliteCommand(
                "UPDATE Users SET LastLogin=@d,FailedLoginAttempts=0,LockoutUntil=NULL WHERE Id=@id", uconn);
            upd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
            upd.Parameters.AddWithValue("@id", userId);
            upd.ExecuteNonQuery();
            CompanyService.MasterAudit(username, "Login");
            return null;
        }

        public static bool VerifyPassword(int userId, string password)
        {
            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("SELECT PasswordHash FROM Users WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", userId);
            var hash = cmd.ExecuteScalar()?.ToString();
            return hash != null && BCrypt.Net.BCrypt.Verify(password, hash);
        }

        /// <summary>Restores a remembered session: loads the active user by id and sets CurrentUser (no password).
        /// Returns true on success. Used by the "Remember me" auto-sign-in at startup.</summary>
        public static bool RestoreSession(int userId)
        {
            try
            {
                using var conn = CompanyService.GetMasterConnection();
                using var cmd = new SqliteCommand("SELECT Id,Username,FullName,Email,Role FROM Users WHERE Id=@id AND IsActive=1", conn);
                cmd.Parameters.AddWithValue("@id", userId);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return false;
                CurrentUser = new User
                {
                    Id = r.GetInt32(0), Username = r.GetString(1),
                    FullName = r.IsDBNull(2) ? "" : r.GetString(2),
                    Email = r.IsDBNull(3) ? "" : r.GetString(3),
                    Role = r.GetString(4)
                };
                CompanyService.MasterAudit(CurrentUser.Username, "Auto Sign-in", "", "Remembered session");
                return true;
            }
            catch { return false; }
        }

        public static void Logout()
        {
            if (CurrentUser != null) CompanyService.MasterAudit(CurrentUser.Username, "Logout");
            SessionService.Clear();   // explicit sign-out forgets the remembered session
            CurrentUser = null;
        }

        // ───────────────────────── Email OTP login (alternative to password) ─────────────────────────

        static User? FindActiveByEmail(string email)
        {
            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("SELECT Id,Username,FullName,Email,Role FROM Users WHERE Email=@e COLLATE NOCASE AND IsActive=1", conn);
            cmd.Parameters.AddWithValue("@e", email.Trim());
            using var r = cmd.ExecuteReader();
            return r.Read()
                ? new User { Id = r.GetInt32(0), Username = r.GetString(1), FullName = r.IsDBNull(2) ? "" : r.GetString(2), Email = r.IsDBNull(3) ? "" : r.GetString(3), Role = r.GetString(4) }
                : null;
        }

        /// <summary>Sends a one-time login code to a registered email. Returns null on success, else an error message.</summary>
        public static async Task<string?> RequestEmailOtpAsync(string email)
        {
            email = (email ?? "").Trim();
            if (email.Length == 0 || !email.Contains('@')) return "Please enter a valid email address.";
            var user = FindActiveByEmail(email);
            if (user == null) return "No active account is registered with that email.";

            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            _otps[email.ToLowerInvariant()] = (code, DateTime.Now.AddMinutes(OtpValidMinutes), 0);

            var html = $@"<div style='font-family:Segoe UI,Arial,sans-serif'>
                <p>Hello {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>
                <p>Your eCheque MICO360 login code is:</p>
                <p style='font-size:26px;font-weight:bold;letter-spacing:4px;color:#8B1818'>{code}</p>
                <p>This code expires in {OtpValidMinutes} minutes. If you didn't request it, ignore this email.</p></div>";
            var (ok, err) = await EmailService.SendAsync(email, user.FullName, "Your eCheque MICO360 login code", html,
                $"Your eCheque MICO360 login code is {code} (expires in {OtpValidMinutes} minutes).");
            if (!ok) { _otps.Remove(email.ToLowerInvariant()); return $"Could not send the code: {err}"; }

            CompanyService.MasterAudit(user.Username, "OTP Requested", email);
            return null;
        }

        /// <summary>Verifies an emailed code and signs the user in. Returns null on success, else an error message.</summary>
        public static string? VerifyEmailOtp(string email, string code)
        {
            var key = (email ?? "").Trim().ToLowerInvariant();
            if (!_otps.TryGetValue(key, out var e)) return "Please request a code first.";
            if (DateTime.Now > e.expiry) { _otps.Remove(key); return "The code has expired. Request a new one."; }
            if (e.attempts >= MaxFailedAttempts) { _otps.Remove(key); return "Too many attempts. Request a new code."; }
            if ((code ?? "").Trim() != e.code) { _otps[key] = (e.code, e.expiry, e.attempts + 1); return "Incorrect code."; }

            _otps.Remove(key);
            var user = FindActiveByEmail(email!.Trim());
            if (user == null) return "Account not found.";

            CurrentUser = user;
            using var conn = CompanyService.GetMasterConnection();
            using var upd = new SqliteCommand("UPDATE Users SET LastLogin=@d,FailedLoginAttempts=0,LockoutUntil=NULL WHERE Id=@id", conn);
            upd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o")); upd.Parameters.AddWithValue("@id", user.Id); upd.ExecuteNonQuery();
            CompanyService.MasterAudit(user.Username, "Login (OTP)");
            return null;
        }

        public static List<User> GetAllUsers()
        {
            var list = new List<User>();
            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("SELECT Id,Username,FullName,Email,Role,IsActive,CreatedDate,FailedLoginAttempts,LockoutUntil FROM Users ORDER BY FullName", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new User
                {
                    Id = r.GetInt32(0), Username = r.GetString(1),
                    FullName = r.IsDBNull(2) ? "" : r.GetString(2),
                    Email = r.IsDBNull(3) ? "" : r.GetString(3),
                    Role = r.GetString(4), IsActive = r.GetInt32(5) == 1,
                    CreatedDate = DateTime.TryParse(r.IsDBNull(6) ? "" : r.GetString(6), out var d) ? d : DateTime.Now,
                    FailedLoginAttempts = r.IsDBNull(7) ? 0 : r.GetInt32(7),
                    LockoutUntil = DateTime.TryParse(r.IsDBNull(8) ? "" : r.GetString(8), out var lu) ? lu : null
                });
            return list;
        }

        public static bool UsernameExists(string username, int excludeId = 0)
        {
            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Username=@u COLLATE NOCASE AND Id!=@id", conn);
            cmd.Parameters.AddWithValue("@u", username.Trim()); cmd.Parameters.AddWithValue("@id", excludeId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public static bool EmailExists(string email, int excludeId = 0)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Email=@e COLLATE NOCASE AND Id!=@id", conn);
            cmd.Parameters.AddWithValue("@e", email.Trim()); cmd.Parameters.AddWithValue("@id", excludeId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        /// <summary>Count of active Admin users — used to prevent removing/demoting the last administrator.</summary>
        public static int ActiveAdminCount(int excludeId = 0)
        {
            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Role='Admin' AND IsActive=1 AND Id!=@id", conn);
            cmd.Parameters.AddWithValue("@id", excludeId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void SaveUser(User user, string? plainPassword = null)
        {
            using var conn = CompanyService.GetMasterConnection();
            var actor = CurrentUser?.Username ?? "SYSTEM";
            if (user.Id == 0)
            {
                // Never fall back to a fixed, known password. If a caller omits one, generate a strong random
                // password that nobody knows — the account can only be accessed after an admin password reset
                // or an email OTP. (The UI already requires a password for new users; this guards direct calls.)
                var h = BCrypt.Net.BCrypt.HashPassword(string.IsNullOrEmpty(plainPassword) ? RandomPassword() : plainPassword);
                using var cmd = new SqliteCommand(
                    "INSERT INTO Users(Username,PasswordHash,FullName,Email,Role,IsActive,CreatedDate,FailedLoginAttempts)VALUES(@u,@h,@fn,@e,@r,@a,@d,0)", conn);
                cmd.Parameters.AddWithValue("@u", user.Username); cmd.Parameters.AddWithValue("@h", h);
                cmd.Parameters.AddWithValue("@fn", user.FullName); cmd.Parameters.AddWithValue("@e", user.Email);
                cmd.Parameters.AddWithValue("@r", user.Role); cmd.Parameters.AddWithValue("@a", user.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o")); cmd.ExecuteNonQuery();
                CompanyService.MasterAudit(actor, "User Created", user.Username, $"Role={user.Role}");
            }
            else
            {
                using var cmd = new SqliteCommand(
                    "UPDATE Users SET FullName=@fn,Email=@e,Role=@r,IsActive=@a WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@fn", user.FullName); cmd.Parameters.AddWithValue("@e", user.Email);
                cmd.Parameters.AddWithValue("@r", user.Role); cmd.Parameters.AddWithValue("@a", user.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", user.Id); cmd.ExecuteNonQuery();
                CompanyService.MasterAudit(actor, "User Updated", user.Username, $"Role={user.Role}, Active={user.IsActive}");
                if (!string.IsNullOrEmpty(plainPassword))
                {
                    using var pw = new SqliteCommand("UPDATE Users SET PasswordHash=@h,FailedLoginAttempts=0,LockoutUntil=NULL WHERE Id=@id", conn);
                    pw.Parameters.AddWithValue("@h", BCrypt.Net.BCrypt.HashPassword(plainPassword));
                    pw.Parameters.AddWithValue("@id", user.Id); pw.ExecuteNonQuery();
                    CompanyService.MasterAudit(actor, "Password Reset", user.Username);
                }
            }
        }

        /// <summary>Self-service: lets the signed-in user update their own name and email (not role or active state).</summary>
        public static string? UpdateOwnProfile(string fullName, string email)
        {
            var me = CurrentUser;
            if (me == null) return "No user is signed in.";
            if (string.IsNullOrWhiteSpace(fullName)) return "Full name is required.";
            if (!string.IsNullOrWhiteSpace(email) && !email.Contains('@')) return "Email address is not valid.";
            if (EmailExists(email, me.Id)) return $"Email '{email}' is already in use.";

            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("UPDATE Users SET FullName=@fn,Email=@e WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@fn", fullName.Trim()); cmd.Parameters.AddWithValue("@e", email.Trim()); cmd.Parameters.AddWithValue("@id", me.Id);
            cmd.ExecuteNonQuery();
            me.FullName = fullName.Trim(); me.Email = email.Trim();   // reflect in the live session
            CompanyService.MasterAudit(me.Username, "Profile Updated (self)", me.Username);
            return null;
        }

        public static bool ChangePassword(int userId, string current, string newPwd)
        {
            if (!VerifyPassword(userId, current)) return false;
            using var conn = CompanyService.GetMasterConnection();
            using var u = new SqliteCommand("UPDATE Users SET PasswordHash=@h WHERE Id=@id", conn);
            u.Parameters.AddWithValue("@h", BCrypt.Net.BCrypt.HashPassword(newPwd));
            u.Parameters.AddWithValue("@id", userId); u.ExecuteNonQuery();
            CompanyService.MasterAudit(CurrentUser?.Username ?? "SYSTEM", "Password Changed", userId.ToString());
            return true;
        }

        /// <summary>A 24-char cryptographically-random password used only when a new user is created without
        /// one — it is intentionally unknowable, forcing an admin reset or OTP before the account can be used.</summary>
        static string RandomPassword()
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var chars = new char[24];
            for (int i = 0; i < chars.Length; i++) chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
            return new string(chars);
        }

        public static void UnlockUser(int userId)
        {
            using var conn = CompanyService.GetMasterConnection();
            using var cmd = new SqliteCommand("UPDATE Users SET FailedLoginAttempts=0,LockoutUntil=NULL WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", userId); cmd.ExecuteNonQuery();
            CompanyService.MasterAudit(CurrentUser?.Username ?? "SYSTEM", "User Unlocked", userId.ToString());
        }
    }
}
