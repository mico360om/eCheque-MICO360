using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Core.Services
{
    /// <summary>Automatic bug/error capture — writes to bugreports.log and the master BugReports table (shared by macOS + Windows).</summary>
    public static class BugReportService
    {
        public static string LogPath => Path.Combine(AppPaths.DataFolder, "bugreports.log");

        public static void Report(Exception? ex, string context = "")
        {
            var message = ex?.Message ?? "Unknown error";
            var detail  = ex?.ToString() ?? "";
            var user    = AuthService.CurrentUser?.Username ?? "-";
            var version = AppInfo.Version;

            try { Directory.CreateDirectory(AppPaths.DataFolder); File.AppendAllText(LogPath, $"===== {DateTime.Now:o} =====\nVersion: {version}\nUser: {user}\nContext: {context}\n{detail}\n\n"); }
            catch { }

            try
            {
                using var conn = CompanyService.GetMasterConnection();
                using var cmd = new SqliteCommand("INSERT INTO BugReports(CreatedDate,AppVersion,UserName,Context,Message,StackTrace)VALUES(@d,@v,@u,@c,@m,@s)", conn);
                cmd.Parameters.AddWithValue("@d", DateTime.Now.ToString("o"));
                cmd.Parameters.AddWithValue("@v", version);
                cmd.Parameters.AddWithValue("@u", user);
                cmd.Parameters.AddWithValue("@c", context);
                cmd.Parameters.AddWithValue("@m", message);
                cmd.Parameters.AddWithValue("@s", detail);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static void OpenLog()
        {
            try
            {
                if (!File.Exists(LogPath)) File.WriteAllText(LogPath, "No issues have been logged yet.\n");
                Process.Start(new ProcessStartInfo(LogPath) { UseShellExecute = true });
            }
            catch { }
        }
    }
}
