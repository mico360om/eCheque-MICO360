using Avalonia.Controls;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
            TxtAppName.Text = AppInfo.AppName;
            TxtVersion.Text = $"Version {AppInfo.Version}  •  {AppInfo.Platform} Edition";
            TxtIntro.Text = DatabaseService.GetSetting("Legal_About_Intro", AppInfo.CompanyIntro);
            TxtCompany.Text = AppInfo.CompanyName;
            TxtEmail.Text = AppInfo.ContactEmail;
            TxtWebsite.Text = AppInfo.Website;
        }
    }
}
