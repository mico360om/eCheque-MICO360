using System.Windows;
using eCheque.MICO360.Services;
using eCheque.MICO360.Views;

namespace eCheque.MICO360
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += (s, ex) =>
            {
                ex.Handled = true;
                MessageBox.Show(
                    $"An unexpected error occurred:\n\n{ex.Exception.Message}\n\nThe application will continue running.",
                    "eCheque MICO360 — Error", MessageBoxButton.OK, MessageBoxImage.Error);
                try { DatabaseService.LogAudit("SYSTEM", "UnhandledException", "", ex.Exception.Message); } catch { }
            };

            System.AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var msg = (ex.ExceptionObject as Exception)?.Message ?? ex.ExceptionObject?.ToString() ?? "Unknown";
                try
                {
                    var folder = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eCheque_MICO360");
                    System.IO.Directory.CreateDirectory(folder);
                    System.IO.File.AppendAllText(System.IO.Path.Combine(folder, "crash.log"),
                        $"{DateTime.Now:o} CRASH: {msg}{Environment.NewLine}");
                }
                catch { }
            };

            try
            {
                CompanyService.Initialize();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize:\n{ex.Message}", "Startup Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
                return;
            }

            Exit += (s, args) =>
            {
                if (!string.IsNullOrEmpty(DatabaseService.DbPath))
                    DatabaseProtectionService.EncryptOnExit(DatabaseService.DbPath);
            };

            var login = new LoginWindow();
            login.Show();
        }
    }
}
