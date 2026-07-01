using Avalonia.Controls;
using Avalonia.Interactivity;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class MyProfileView : UserControl
    {
        public MyProfileView() => InitializeComponent();

        void OnChangePassword(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not MyProfileViewModel vm) return;
            vm.ChangePassword(PwdCurrent.Text ?? "", PwdNew.Text ?? "", PwdConfirm.Text ?? "");
            PwdCurrent.Text = ""; PwdNew.Text = ""; PwdConfirm.Text = "";
        }
    }
}
