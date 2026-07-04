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

            // "Remember me": mark the app as used so the 30-day inactivity window keeps resetting.
            Closing += (_, _) => SessionService.Touch();

            NavDashboard(this, new RoutedEventArgs());

            // Auto-check for updates on startup and notify the user if one is available.
            _ = CheckForUpdatesOnStartupAsync();

            // Send PDC due reminders if enabled and due (respects the user's chosen frequency).
            _ = PdcReminderService.MaybeSendAsync();
            // Start background cloud sync (no-op unless enabled + this device is registered).
            SyncService.StartBackground();
            // Keep the sidebar connection indicator fresh.
            StartSyncStatusIndicator();
        }

        DispatcherTimer? _syncStatusTimer;

        void StartSyncStatusIndicator()
        {
            UpdateSyncStatus();
            _syncStatusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _syncStatusTimer.Tick += (_, _) => UpdateSyncStatus();
            _syncStatusTimer.Start();
        }

        void UpdateSyncStatus()
        {
            if (SyncDot == null) return;
            string color, label, sub;
            switch (SyncService.ConnectionStatus)
            {
                case Sync.Client.SyncConnState.Connected:
                    color = "#2ECC71"; label = "Connected";
                    var t = SyncService.LastSyncLocal;
                    sub = t != null ? $"synced {t:HH:mm}" : "online";
                    break;
                case Sync.Client.SyncConnState.Disconnected:
                    color = "#E74C3C"; label = "Disconnected"; sub = "server unreachable"; break;
                case Sync.Client.SyncConnState.NotConnected:
                    color = "#E67E22"; label = "Not connected"; sub = "set up in Settings"; break;
                default:
                    color = "#888888"; label = "Local only"; sub = "cloud sync off"; break;
            }
            SyncDot.Fill = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse(color));
            SyncStatusText.Text = label;
            SyncStatusSub.Text = sub;
            var last = SyncService.LastResult;
            ToolTip.SetTip(SyncStatusBar, string.IsNullOrWhiteSpace(last)
                ? "Cloud sync status — click for settings"
                : $"{label} — click for Sync settings\n{last}");
        }

        void SyncStatus_Click(object? sender, Avalonia.Input.PointerReleasedEventArgs e)
            => NavSettings(this, new RoutedEventArgs());

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

        void NavProfiles(object? sender, RoutedEventArgs e)
        {
            SetTitle("Cheque Profiles");
            var vm = new ProfileManagerViewModel(); vm.Load();
            ContentArea.Content = new ProfileManagerView(vm);
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
