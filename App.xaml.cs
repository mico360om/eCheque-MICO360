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

            string _lastMsg = ""; DateTime _lastAt = DateTime.MinValue;

            DispatcherUnhandledException += (s, ex) =>
            {
                ex.Handled = true;
                BugReportService.Report(ex.Exception, "DispatcherUnhandledException");
                // Auto-logged; show a friendly notice, de-duplicated so a repeating error can't spam popups.
                var m = ex.Exception.Message;
                if (m != _lastMsg || (DateTime.Now - _lastAt).TotalSeconds > 5)
                {
                    _lastMsg = m; _lastAt = DateTime.Now;
                    MessageBox.Show(
                        "Something went wrong, but the app will keep running.\n\nThe problem has been logged automatically for the developers.",
                        "eCheque MICO360", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            System.AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
                BugReportService.Report(ex.ExceptionObject as Exception, "AppDomain.UnhandledException (fatal)");

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

            // Databases are encrypted at rest by SQLCipher (SecurityService), so the legacy
            // DPAPI re-wrap on exit is no longer needed (and would conflict with SQLCipher).

            // "Remember me": if a valid remembered session exists (used within the last 30 days),
            // sign in automatically and go straight to the app; otherwise show the login screen.
            var remembered = SessionService.RememberedUserId();
            if (remembered != null && AuthService.RestoreSession(remembered.Value))
            {
                try
                {
                    var company = CompanyService.GetAll().FirstOrDefault();
                    if (company != null)
                    {
                        CompanyService.OpenCompany(company.Id, company.Name);
                        SessionService.Touch();
                        new MainWindow().Show();
                        return;
                    }
                }
                catch (Exception ex) { BugReportService.Report(ex, "Remembered auto sign-in"); }
                AuthService.Logout();   // couldn't complete auto sign-in → fall back to login
            }

            var login = new LoginWindow();
            login.Show();
        }
    }
}
