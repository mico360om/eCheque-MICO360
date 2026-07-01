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
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Master company DB is initialized before login (per-company DB opens after company selection).
                try { CompanyService.Initialize(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine("Init error: " + ex.Message); }

                desktop.MainWindow = new LoginWindow();
            }
            base.OnFrameworkInitializationCompleted();
        }
    }
}
