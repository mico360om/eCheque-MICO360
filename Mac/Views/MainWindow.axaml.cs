using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class MainWindow : Window
    {
        readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
        bool _initializingCompanies;

        public MainWindow()
        {
            InitializeComponent();
            TxtCompany.Text = CompanyService.CurrentCompanyName;
            TxtUser.Text = $"{AuthService.CurrentUser?.FullName} ({AuthService.CurrentUser?.Role})";
            PopulateCompanySwitcher();

            if (!AuthService.CanEdit) BtnNewCheque.IsVisible = false;   // read-only role
            if (!AuthService.IsAdmin) { BtnAudit.IsVisible = false; BtnCompanies.IsVisible = false; BtnUsers.IsVisible = false; TxtAdminHdr.IsVisible = false; }

            _clock.Tick += (s, e) => TxtClock.Text = DateTime.Now.ToString("dddd, dd MMM yyyy  HH:mm:ss");
            _clock.Start();

            NavDashboard(this, new RoutedEventArgs());

            // Auto-check for updates on startup and notify the user if one is available.
            _ = CheckForUpdatesOnStartupAsync();
        }

        void PopulateCompanySwitcher()
        {
            _initializingCompanies = true;
            var companies = CompanyService.GetAll();
            CmbCompanySwitch.ItemsSource = companies;
            CmbCompanySwitch.SelectedItem = companies.FirstOrDefault(c => c.Id == CompanyService.CurrentCompanyId);
            CmbCompanySwitch.IsVisible = companies.Count > 1;   // only when there's more than one
            _initializingCompanies = false;
        }

        void OnCompanySwitch(object? sender, SelectionChangedEventArgs e)
        {
            if (_initializingCompanies) return;
            if (CmbCompanySwitch.SelectedItem is not Company c || c.Id == CompanyService.CurrentCompanyId) return;
            CompanyService.OpenCompany(c.Id, c.Name);
            TxtCompany.Text = c.Name;
            NavDashboard(this, new RoutedEventArgs());
        }

        async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                var info = await UpdateService.CheckForUpdatesAsync();
                if (!info.UpdateAvailable) return;

                var body = info.Mandatory
                    ? $"A required update ({info.LatestVersion}) is available. You have {info.CurrentVersion}.\n\nPlease update to continue using the latest version."
                    : $"Update {info.LatestVersion} is available (you have {info.CurrentVersion}).\n\nWould you like to get it now?";

                var getIt = await MessageDialog.Show(this, "Update Available", body, "Get Update", info.Mandatory ? "Remind me later" : "Later");
                if (getIt)
                    Process.Start(new ProcessStartInfo(Core.Services.AppInfo.RepoUrl + "/releases/latest") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                UpdateService.Log($"Startup check skipped: {ex.Message}");
            }
        }

        void SetTitle(string t) => TxtTitle.Text = t;

        void NavDashboard(object? sender, RoutedEventArgs e)
        {
            SetTitle("Dashboard");
            var vm = new DashboardViewModel(); vm.Load();
            ContentArea.Content = new DashboardView { DataContext = vm };
        }

        void NavNewCheque(object? sender, RoutedEventArgs e)
        {
            SetTitle("New Cheque");
            var vm = new ChequeEntryViewModel();
            vm.Saved += () => NavHistory(this, e);
            vm.Cancelled += () => NavHistory(this, e);
            ContentArea.Content = new ChequeEntryView { DataContext = vm };
        }

        void NavHistory(object? sender, RoutedEventArgs e)
        {
            SetTitle("Cheque History");
            var vm = new ChequeHistoryViewModel();
            ContentArea.Content = new ChequeHistoryView { DataContext = vm };
        }

        void NavTracking(object? sender, RoutedEventArgs e)
        {
            SetTitle("Cheque Tracking");
            var vm = new ChequeTrackingViewModel();
            ContentArea.Content = new ChequeTrackingView { DataContext = vm };
        }

        void NavMyProfile(object? sender, RoutedEventArgs e)
        {
            SetTitle("My Profile");
            ContentArea.Content = new MyProfileView { DataContext = new MyProfileViewModel() };
        }

        void NavAbout(object? sender, RoutedEventArgs e)
        {
            SetTitle("About Us");
            ContentArea.Content = new AboutView();
        }

        void NavPrintHistory(object? sender, RoutedEventArgs e)
        {
            SetTitle("Print History");
            ContentArea.Content = new PrintHistoryView { DataContext = new PrintHistoryViewModel() };
        }

        void NavAudit(object? sender, RoutedEventArgs e)
        {
            SetTitle("Audit Log");
            var vm = new AuditLogViewModel(); vm.Load();
            ContentArea.Content = new AuditLogView { DataContext = vm };
        }

        void NavUpdates(object? sender, RoutedEventArgs e)
        {
            SetTitle("Software Updates");
            var vm = new UpdateViewModel(); vm.Load();
            ContentArea.Content = new UpdateView { DataContext = vm };
        }

        void NavLegal(object? sender, RoutedEventArgs e)
        {
            SetTitle("Terms & Legal");
            var vm = new LegalViewModel(LegalKind.Terms);
            vm.BackRequested += () => NavDashboard(this, e);
            vm.Load();
            ContentArea.Content = new LegalView { DataContext = vm };
        }

        void NavSettings(object? sender, RoutedEventArgs e)
        {
            SetTitle("Settings");
            var vm = new SettingsViewModel(); vm.Load();
            ContentArea.Content = new SettingsView { DataContext = vm };
        }

        void NavUserManagement(object? sender, RoutedEventArgs e)
        {
            SetTitle("User Management");
            var vm = new UserManagementViewModel(); vm.Load();
            ContentArea.Content = new UserManagementView { DataContext = vm };
        }

        void NavCompanies(object? sender, RoutedEventArgs e)
        {
            SetTitle("Company Manager");
            var vm = new CompanyManagerViewModel();
            vm.CompanyListChanged += () => { PopulateCompanySwitcher(); TxtCompany.Text = CompanyService.CurrentCompanyName; };
            vm.Load();
            ContentArea.Content = new CompanyManagerView { DataContext = vm };
        }

        void OnLogout(object? sender, RoutedEventArgs e)
        {
            _clock.Stop();
            AuthService.Logout();
            new LoginWindow().Show();
            Close();
        }
    }
}
