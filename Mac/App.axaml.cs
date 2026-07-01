using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using eCheque.MICO360.Core.Services;
using eCheque.MICO360.Mac.Views;

namespace eCheque.MICO360.Mac
{
    public partial class App : Application
    {
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        public override void OnFrameworkInitializationCompleted()
        {
            // Automatic bug reporting for uncaught errors.
            AppDomain.CurrentDomain.UnhandledException += (s, e) => BugReportService.Report(e.ExceptionObject as Exception, "AppDomain.UnhandledException");
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) => { BugReportService.Report(e.Exception, "UnobservedTaskException"); e.SetObserved(); };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Master company DB is initialized before login (per-company DB opens after company selection).
                try { CompanyService.Initialize(); }
                catch (Exception ex) { BugReportService.Report(ex, "CompanyService.Initialize"); }

                desktop.MainWindow = new LoginWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
