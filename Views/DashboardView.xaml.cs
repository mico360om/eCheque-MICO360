using System.Windows.Controls;
using eCheque.MICO360.Services;
using eCheque.MICO360.ViewModels;
namespace eCheque.MICO360.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                RunUser.Text = AuthService.CurrentUser?.FullName ?? AuthService.CurrentUser?.Username ?? "";
                RunCo.Text = CompanyService.CurrentCompanyName.Length > 0
                    ? CompanyService.CurrentCompanyName
                    : DatabaseService.GetSetting("CompanyName", "");
                if (DataContext is DashboardViewModel vm) vm.Load();
            };
        }
    }
}
