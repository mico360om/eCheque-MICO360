using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using eCheque.MICO360.Core.Services;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class MainWindow : Window
    {
        readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };

        public MainWindow()
        {
            InitializeComponent();
            TxtCompany.Text = CompanyService.CurrentCompanyName;
            TxtUser.Text = $"{AuthService.CurrentUser?.FullName} ({AuthService.CurrentUser?.Role})";

            if (!AuthService.CanEdit) BtnNewCheque.IsVisible = false;   // read-only role

            _clock.Tick += (s, e) => TxtClock.Text = DateTime.Now.ToString("dddd, dd MMM yyyy  HH:mm:ss");
            _clock.Start();

            NavDashboard(this, new RoutedEventArgs());
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

        void NavAbout(object? sender, RoutedEventArgs e)
        {
            SetTitle("About Us");
            ContentArea.Content = new AboutView();
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
