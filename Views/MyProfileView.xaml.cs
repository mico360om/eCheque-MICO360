using System.Windows.Controls;
using eCheque.MICO360.ViewModels;

namespace eCheque.MICO360.Views
{
    public partial class MyProfileView : UserControl
    {
        public MyProfileView() => InitializeComponent();

        private void BtnChangePassword_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is not MyProfileViewModel vm) return;
            vm.ChangePassword(PwdCurrent.Password, PwdNew.Password, PwdConfirm.Password);
            PwdCurrent.Clear(); PwdNew.Clear(); PwdConfirm.Clear();
        }
    }
}
