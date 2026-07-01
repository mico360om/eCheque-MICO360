using System.Diagnostics;
using System.IO;
using Microsoft.Data.Sqlite;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Automatic bug/error capture. Every unhandled exception (and any error we choose to report) is
    /// written to bugreports.log AND stored in a BugReports table in the master database, tagged with
    /// app version, user, and context — so problems can be reviewed and fixed later.
    /// </summary>
    public static class BugReportService
    {
        static string Folder
        {
            get
            {
                var f = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
                Directory.CreateDirectory(f);
                return f;
            }
        }

        public static string LogPath => Path.Combine(Folder, "bugreports.log");

        public static void Report(Exception? ex, string context = "")
        {
            var message = ex?.Message ?? "Unknown error";
            var detail  = ex?.ToString() ?? "";
            var user    = AuthService.CurrentUser?.Username ?? "-";
            var version = Helpers.AppInfo.Version;

            try { File.AppendAllText(LogPath, $"===== {DateTime.Now:o} =====\nVersion: {version}\nUser: {user}\nContext: {context}\n{detail}\n\n"); }
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
