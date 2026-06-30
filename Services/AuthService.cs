using Microsoft.Data.Sqlite;
using eCheque.MICO360.Models;

namespace eCheque.MICO360.Services
{
    public static class AuthService
    {
        private const int MaxFailedAttempts = 5;
        private const int LockoutMinutes = 5;

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

            using (var conn = DatabaseService.GetConnection())
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
                DatabaseService.LogAudit(username, "Login Failed", "", "User not found");
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
                using var wconn = DatabaseService.GetConnection();
                if (failedAttempts >= MaxFailedAttempts)
                {
                    var newLockUntil = DateTime.Now.AddMinutes(LockoutMinutes);
                    using var lk = new SqliteCommand(
                        "UPDATE Users SET FailedLoginAttempts=@fa,LockoutUntil=@lu WHERE Id=@id", wconn);
                    lk.Parameters.AddWithValue("@fa", failedAttempts);
                    lk.Parameters.AddWithValue("@lu", newLockUntil.ToString("o"));
                    lk.Parameters.AddWithValue("@id", userId);
                    lk.ExecuteNonQuery();
                    DatabaseService.LogAudit(username, "Account Locked", "", $"After {failedAttempts} failed attempts");
                    return $"Account locked for {LockoutMinutes} minutes after {MaxFailedAttempts} failed attempts.";
                }
                else
                {
                    using var fa = new SqliteCommand("UPDATE Users SET FailedLoginAttempts=@fa WHERE Id=@id", wconn);
                    fa.Parameters.AddWithValue("@fa", failedAttempts);
                    fa.Parameters.AddWithValue("@id", userId);
                    fa.ExecuteNonQuery();
                    int remaining = MaxFailedAttempts - failedAttempts;
                    DatabaseService.LogAudit(username, "Login Failed", "", $"Attempt {failedAttempts}/{MaxFailedAttempts}");
                    return $"Invalid password. {remaining} attempt(s) remaining before lockout.";
                }
            }

            // --- Step 4: Success — set session, reset lockout counters ---
            CurrentUser = new User { Id = userId, Username = dbUsername, FullName = fullName, Email = email, Role = role };
            using var uconn = DatabaseService.GetConnection();
            using var upd = new SqliteCommand(
                "UPDATE Users SET LastLogin=@d,FailedLoginAttempts=0,LockoutUntil=NULL WHERE Id=@id", uconn);
            upd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
            upd.Parameters.AddWithValue("@id", userId);
            upd.ExecuteNonQuery();
            DatabaseService.LogAudit(username, "Login");
            return null;
        }

        public static bool VerifyPassword(int userId, string password)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("SELECT PasswordHash FROM Users WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", userId);
            var hash = cmd.ExecuteScalar()?.ToString();
            return hash != null && BCrypt.Net.BCrypt.Verify(password, hash);
        }

        public static void Logout()
        {
            if (CurrentUser != null) DatabaseService.LogAudit(CurrentUser.Username, "Logout");
            CurrentUser = null;
        }

        public static List<User> GetAllUsers()
        {
            var list = new List<User>();
            using var conn = DatabaseService.GetConnection();
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
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Username=@u COLLATE NOCASE AND Id!=@id", conn);
            cmd.Parameters.AddWithValue("@u", username.Trim()); cmd.Parameters.AddWithValue("@id", excludeId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public static bool EmailExists(string email, int excludeId = 0)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Email=@e COLLATE NOCASE AND Id!=@id", conn);
            cmd.Parameters.AddWithValue("@e", email.Trim()); cmd.Parameters.AddWithValue("@id", excludeId);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        /// <summary>Count of active Admin users — used to prevent removing/demoting the last administrator.</summary>
        public static int ActiveAdminCount(int excludeId = 0)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("SELECT COUNT(*) FROM Users WHERE Role='Admin' AND IsActive=1 AND Id!=@id", conn);
            cmd.Parameters.AddWithValue("@id", excludeId);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void SaveUser(User user, string? plainPassword = null)
        {
            using var conn = DatabaseService.GetConnection();
            var actor = CurrentUser?.Username ?? "SYSTEM";
            if (user.Id == 0)
            {
                var h = BCrypt.Net.BCrypt.HashPassword(plainPassword ?? "Change@123");
                using var cmd = new SqliteCommand(
                    "INSERT INTO Users(Username,PasswordHash,FullName,Email,Role,IsActive,CreatedDate,FailedLoginAttempts)VALUES(@u,@h,@fn,@e,@r,@a,@d,0)", conn);
                cmd.Parameters.AddWithValue("@u", user.Username); cmd.Parameters.AddWithValue("@h", h);
                cmd.Parameters.AddWithValue("@fn", user.FullName); cmd.Parameters.AddWithValue("@e", user.Email);
                cmd.Parameters.AddWithValue("@r", user.Role); cmd.Parameters.AddWithValue("@a", user.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o")); cmd.ExecuteNonQuery();
                DatabaseService.LogAudit(actor, "User Created", user.Username, $"Role={user.Role}");
            }
            else
            {
                using var cmd = new SqliteCommand(
                    "UPDATE Users SET FullName=@fn,Email=@e,Role=@r,IsActive=@a WHERE Id=@id", conn);
                cmd.Parameters.AddWithValue("@fn", user.FullName); cmd.Parameters.AddWithValue("@e", user.Email);
                cmd.Parameters.AddWithValue("@r", user.Role); cmd.Parameters.AddWithValue("@a", user.IsActive ? 1 : 0);
                cmd.Parameters.AddWithValue("@id", user.Id); cmd.ExecuteNonQuery();
                DatabaseService.LogAudit(actor, "User Updated", user.Username, $"Role={user.Role}, Active={user.IsActive}");
                if (!string.IsNullOrEmpty(plainPassword))
                {
                    using var pw = new SqliteCommand("UPDATE Users SET PasswordHash=@h,FailedLoginAttempts=0,LockoutUntil=NULL WHERE Id=@id", conn);
                    pw.Parameters.AddWithValue("@h", BCrypt.Net.BCrypt.HashPassword(plainPassword));
                    pw.Parameters.AddWithValue("@id", user.Id); pw.ExecuteNonQuery();
                    DatabaseService.LogAudit(actor, "Password Reset", user.Username);
                }
            }
        }

        public static bool ChangePassword(int userId, string current, string newPwd)
        {
            if (!VerifyPassword(userId, current)) return false;
            using var conn = DatabaseService.GetConnection();
            using var u = new SqliteCommand("UPDATE Users SET PasswordHash=@h WHERE Id=@id", conn);
            u.Parameters.AddWithValue("@h", BCrypt.Net.BCrypt.HashPassword(newPwd));
            u.Parameters.AddWithValue("@id", userId); u.ExecuteNonQuery();
            DatabaseService.LogAudit(CurrentUser?.Username ?? "SYSTEM", "Password Changed", userId.ToString());
            return true;
        }

        public static void UnlockUser(int userId)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("UPDATE Users SET FailedLoginAttempts=0,LockoutUntil=NULL WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", userId); cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(CurrentUser?.Username ?? "SYSTEM", "User Unlocked", userId.ToString());
        }
    }
}
