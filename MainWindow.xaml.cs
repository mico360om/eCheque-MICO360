using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using eCheque.MICO360.Services;
using CompanyService = eCheque.MICO360.Services.CompanyService;
using eCheque.MICO360.ViewModels;
using eCheque.MICO360.Views;

namespace eCheque.MICO360
{
    public partial class MainWindow : Window
    {
        private Button? _activeNav;
        private bool _initializingCompanies;
        private readonly DispatcherTimer _timer = new();
        private readonly DispatcherTimer _idleTimer = new();
        private DateTime _lastActivity = DateTime.Now;

        public MainWindow()
        {
            InitializeComponent();
            TxtCompanyName.Text = CompanyService.CurrentCompanyName;
            TxtUserInfo.Text = $"{AuthService.CurrentUser?.FullName} ({AuthService.CurrentUser?.Role})";
            PopulateCompanySwitcher();

            if (!AuthService.IsAdmin)
            {
                NavCompanies.Visibility = Visibility.Collapsed;
                NavUsers.Visibility = Visibility.Collapsed;
                NavAudit.Visibility = Visibility.Collapsed;
                TxtAdminHdr.Visibility = Visibility.Collapsed;
            }

            // Read-only roles (Viewer/Auditor) cannot create cheques.
            if (!AuthService.CanEdit)
                NavNewCheque.Visibility = Visibility.Collapsed;

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (s, e) => TxtDateTime.Text = DateTime.Now.ToString("dddd, dd MMM yyyy  HH:mm:ss");
            _timer.Start();

            var timeoutMin = int.TryParse(DatabaseService.GetSetting("SessionTimeoutMinutes", "10"), out var t) ? t : 10;
            _idleTimer.Interval = TimeSpan.FromSeconds(30);
            _idleTimer.Tick += (s, e) => CheckIdle(timeoutMin);
            _idleTimer.Start();

            PreviewMouseMove += (s, e) => _lastActivity = DateTime.Now;
            PreviewKeyDown  += (s, e) => _lastActivity = DateTime.Now;

            // Enter key on the lock overlay password box
            PwdUnlock.KeyDown += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter) BtnUnlock_Click(s, e);
            };

            Navigate("Dashboard");
            SetActiveNav(NavDashboard);

            // Silent background update check on startup.
            _ = CheckForUpdatesOnStartupAsync();
        }

        private async System.Threading.Tasks.Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                var info = await UpdateService.CheckForUpdatesAsync();
                if (!info.UpdateAvailable) return;

                if (info.Mandatory)
                {
                    MessageBox.Show(
                        $"A required update ({info.LatestVersion}) is available and must be installed.\n\nYou will be taken to the update screen.",
                        "Required Update", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Navigate("Updates"); SetActiveNav(NavUpdates);
                }
                else
                {
                    var r = MessageBox.Show(
                        $"Update {info.LatestVersion} is available (you have {info.CurrentVersion}).\n\nWould you like to view it now?",
                        "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (r == MessageBoxResult.Yes) { Navigate("Updates"); SetActiveNav(NavUpdates); }
                }
            }
            catch (Exception ex)
            {
                // Never block startup on update problems — just log.
                UpdateService.Log($"Startup check skipped: {ex.Message}");
            }
        }

        private void PopulateCompanySwitcher()
        {
            _initializingCompanies = true;
            var companies = CompanyService.GetAll();
            CmbCompanySwitch.ItemsSource = companies;
            foreach (var c in companies)
                if (c.Id == CompanyService.CurrentCompanyId) { CmbCompanySwitch.SelectedItem = c; break; }
            // Only show the switcher when there is more than one company to move between.
            CmbCompanySwitch.Visibility = companies.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            _initializingCompanies = false;
        }

        private void CmbCompanySwitch_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_initializingCompanies) return;
            if (CmbCompanySwitch.SelectedItem is not Models.Company c || c.Id == CompanyService.CurrentCompanyId) return;

            if (MessageBox.Show($"Switch to '{c.Name}'?\n\nAny unsaved changes on the current screen will be discarded.",
                    "Switch Company", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                // Revert the dropdown to the current company without re-triggering a switch.
                _initializingCompanies = true;
                foreach (var item in CmbCompanySwitch.Items)
                    if (item is Models.Company cc && cc.Id == CompanyService.CurrentCompanyId) { CmbCompanySwitch.SelectedItem = item; break; }
                _initializingCompanies = false;
                return;
            }
            try
            {
                CompanyService.OpenCompany(c.Id, c.Name);
                TxtCompanyName.Text = c.Name;
                Navigate("Dashboard"); SetActiveNav(NavDashboard);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not switch company: {ex.Message}", "Company Switch", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckIdle(int timeoutMin)
        {
            if (LockOverlay.Visibility == Visibility.Visible) return;
            if ((DateTime.Now - _lastActivity).TotalMinutes >= timeoutMin)
                LockOverlay.Visibility = Visibility.Visible;
        }

        private void BtnUnlock_Click(object sender, RoutedEventArgs e)
        {
            TxtUnlockError.Visibility = Visibility.Collapsed;
            var pwd = PwdUnlock.Password;
            if (AuthService.CurrentUser == null || !AuthService.VerifyPassword(AuthService.CurrentUser.Id, pwd))
            {
                TxtUnlockError.Text = "Incorrect password.";
                TxtUnlockError.Visibility = Visibility.Visible;
                PwdUnlock.Clear();
                return;
            }
            LockOverlay.Visibility = Visibility.Collapsed;
            PwdUnlock.Clear();
            _lastActivity = DateTime.Now;
        }

        private void NavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                Navigate(btn.Tag?.ToString() ?? "");
                SetActiveNav(btn);
            }
        }

        private void SetActiveNav(Button btn)
        {
            if (_activeNav != null) _activeNav.Background = System.Windows.Media.Brushes.Transparent;
            _activeNav = btn;
            btn.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x8B, 0x18, 0x18));
        }

        private static readonly string[] AdminOnlyPages = { "Companies", "Users", "Audit" };

        private void Navigate(string page)
        {
            // Authorization guard — admin-only modules are blocked at the navigation
            // layer, not merely hidden in the sidebar (defense in depth).
            if (Array.IndexOf(AdminOnlyPages, page) >= 0 && !AuthService.IsAdmin)
            {
                DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Access Denied", page);
                MessageBox.Show("You do not have permission to access this module.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Read-only roles cannot open the cheque entry/creation screen.
            if (page == "NewCheque" && !AuthService.CanEdit)
            {
                MessageBox.Show("Your role has read-only access and cannot create cheques.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TxtPageTitle.Text = page switch
            {
                "Dashboard" => "Dashboard",
                "NewCheque" => "New Cheque",
                "History" => "Cheque History",
                "Tracking" => "Cheque Tracking",
                "PrintHistory" => "Print History",
                "Profiles" => "Cheque Profiles",
                "MyProfile" => "My Profile",
                "Settings" => "Settings",
                "Companies" => "Company Manager",
                "Users" => "User Management",
                "Audit" => "Audit Log",
                "Updates" => "Software Updates",
                "Terms" => "Terms & Conditions",
                "Privacy" => "Privacy Policy",
                "About" => "About Us",
                _ => page
            };

            try
            {
                UserControl? view = page switch
                {
                    "Dashboard" => CreateDashboard(),
                    "NewCheque" => CreateChequeEntry(),
                    "History" => CreateHistory(),
                    "Tracking" => CreateTracking(),
                    "PrintHistory" => CreatePrintHistory(),
                    "Profiles" => CreateProfiles(),
                    "MyProfile" => new MyProfileView { DataContext = new MyProfileViewModel() },
                    "Settings" => CreateSettings(),
                    "Companies" => CreateCompanies(),
                    "Users" => CreateUsers(),
                    "Audit" => CreateAudit(),
                    "Updates" => CreateUpdates(),
                    "Terms" => CreateLegal(ViewModels.LegalKind.Terms),
                    "Privacy" => CreateLegal(ViewModels.LegalKind.Privacy),
                    "About" => CreateAbout(),
                    _ => null
                };
                if (view != null) MainContent.Content = view;
            }
            catch (Exception ex)
            {
                var err = new System.Windows.Controls.TextBlock
                {
                    Text = $"Error loading {page}:\n\n{ex.Message}",
                    Foreground = System.Windows.Media.Brushes.Red,
                    Margin = new Thickness(24),
                    FontSize = 13,
                    TextWrapping = System.Windows.TextWrapping.Wrap
                };
                MainContent.Content = err;
                try { DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "SYSTEM", "NavError", page, ex.Message); } catch { }
            }
        }

        /// <summary>
        /// Shared print orchestration: blocks cancelled/void cheques, requires a mandatory
        /// reason on reprint, opens the preview, and records the print + reason on success.
        /// Returns true if the cheque was actually printed.
        /// </summary>
        private bool RunPrintFlow(Models.ChequeRecord cheque, Models.ChequeProfile profile)
        {
            if (!AuthService.CanEdit)
            {
                MessageBox.Show("Your role has read-only access and cannot print cheques.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            if (ChequeService.IsPrintBlocked(cheque.Status))
            {
                MessageBox.Show($"Cheque #{cheque.ChequeNumber} is {cheque.Status} and cannot be printed.",
                    "Print Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            bool isReprint = cheque.PrintCount > 0;
            string reason = "";
            if (isReprint)
            {
                var warn = DatabaseService.GetSetting("WarnOnReprint", "true") == "true";
                if (warn && MessageBox.Show(
                    $"Cheque #{cheque.ChequeNumber} has already been printed {cheque.PrintCount} time(s).\nDo you want to reprint?",
                    "Reprint Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                    return false;

                var r = Helpers.InputBox.Show($"Reprint reason for cheque #{cheque.ChequeNumber}:", "Reprint Reason");
                if (r == null) return false;                       // user cancelled
                reason = r;
            }

            var preview = new PrintPreviewWindow(cheque, profile);
            preview.ShowDialog();
            if (!preview.WasPrinted) return false;

            ChequeService.RecordPrint(cheque.Id, cheque.ChequeNumber, isReprint, reason);
            ChequeService.UpdateStatus(cheque.Id, isReprint ? "Reprinted" : "Printed", preview.PdfPath);
            return true;
        }

        private UserControl CreateDashboard()
        {
            var vm = new DashboardViewModel();
            vm.NewChequeRequested    += () => { Navigate("NewCheque"); SetActiveNav(NavNewCheque); };
            vm.HistoryRequested      += () => { Navigate("History");   SetActiveNav(NavHistory);   };
            vm.PendingChequesRequested += () => { ShowHistoryFiltered("Draft"); };
            vm.PdcRequested += () => { Navigate("Tracking"); SetActiveNav(NavTracking); };
            vm.PrintRequested += (cheque, profile) =>
            {
                if (RunPrintFlow(cheque, profile)) vm.Load();
            };
            return new DashboardView { DataContext = vm };
        }

        /// <summary>Navigates to History pre-filtered by status (e.g. the dashboard "pending" card).</summary>
        private void ShowHistoryFiltered(string status)
        {
            var vm = new ChequeHistoryViewModel();
            vm.PrintRequested += (cheque, profile) => { if (RunPrintFlow(cheque, profile)) vm.Load(); };
            vm.EditRequested += HandleEditRequested;
            vm.Load();
            vm.StatusFilter = status;
            TxtPageTitle.Text = "Cheque History";
            MainContent.Content = new ChequeHistoryView { DataContext = vm };
            SetActiveNav(NavHistory);
        }

        private UserControl CreateChequeEntry(int? id = null)
        {
            var vm = new ChequeEntryViewModel(); vm.Load(id);
            vm.PrintRequested += (cheque, profile) =>
            {
                if (RunPrintFlow(cheque, profile)) { Navigate("History"); SetActiveNav(NavHistory); }
            };
            vm.PreviewRequested += (cheque, profile) =>
            {
                if (RunPrintFlow(cheque, profile)) { Navigate("History"); SetActiveNav(NavHistory); }
            };
            vm.Saved      += () => { Navigate("History"); SetActiveNav(NavHistory); };
            vm.Cancelled  += () => { Navigate("History"); SetActiveNav(NavHistory); };
            return new ChequeEntryView { DataContext = vm };
        }

        private UserControl CreateHistory()
        {
            var vm = new ChequeHistoryViewModel(); vm.Load();
            vm.PrintRequested += (cheque, profile) => { if (RunPrintFlow(cheque, profile)) vm.Load(); };
            vm.EditRequested += HandleEditRequested;
            return new ChequeHistoryView { DataContext = vm };
        }

        /// <summary>Opens a cheque for editing, blocking issued/closed cheques for non-admins.</summary>
        private void HandleEditRequested(int id)
        {
            if (!AuthService.CanEdit)
            {
                MessageBox.Show("Your role has read-only access and cannot edit cheques.",
                    "Access Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var cheque = ChequeService.GetCheque(id);
            if (cheque != null && ChequeService.IsLocked(cheque.Status) && !AuthService.IsAdmin)
            {
                MessageBox.Show($"Cheque #{cheque.ChequeNumber} is {cheque.Status} and can only be edited by an administrator.",
                    "Edit Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var entry = CreateChequeEntry(id);
            MainContent.Content = entry;
            TxtPageTitle.Text = "Edit Cheque";
        }

        private UserControl CreateProfiles()
        {
            var vm = new ProfileManagerViewModel(); vm.Load();
            return new ProfileManagerView { DataContext = vm };
        }

        private UserControl CreateSettings()
        {
            var vm = new SettingsViewModel(); vm.Load();
            return new SettingsView { DataContext = vm };
        }

        private UserControl CreateUsers()
        {
            var vm = new UserManagementViewModel(); vm.Load();
            return new UserManagementView { DataContext = vm };
        }

        private UserControl CreateCompanies()
        {
            var vm = new CompanyManagerViewModel(); vm.Load();
            return new CompanyManagerView { DataContext = vm };
        }

        private UserControl CreatePrintHistory()
        {
            var vm = new PrintHistoryViewModel(); vm.Load();
            return new PrintHistoryView { DataContext = vm };
        }

        private UserControl CreateTracking()
        {
            var vm = new ChequeTrackingViewModel(); vm.Load();
            return new ChequeTrackingView { DataContext = vm };
        }

        private UserControl CreateAudit()
        {
            var vm = new AuditLogViewModel(); vm.Load();
            return new AuditLogView { DataContext = vm };
        }

        private UserControl CreateUpdates()
        {
            return new UpdateView { DataContext = new UpdateViewModel() };
        }

        private UserControl CreateLegal(ViewModels.LegalKind kind)
        {
            var vm = new LegalViewModel(kind); vm.Load();
            vm.BackRequested += () => { Navigate("Dashboard"); SetActiveNav(NavDashboard); };
            return new LegalView { DataContext = vm };
        }

        private UserControl CreateAbout()
        {
            var vm = new AboutViewModel(); vm.Load();
            vm.BackRequested += () => { Navigate("Dashboard"); SetActiveNav(NavDashboard); };
            return new AboutView { DataContext = vm };
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            AuthService.Logout();
            var login = new LoginWindow();
            login.Show();
            Close();
        }

        protected override void OnClosed(EventArgs e) { _timer.Stop(); _idleTimer.Stop(); base.OnClosed(e); }
    }
}
