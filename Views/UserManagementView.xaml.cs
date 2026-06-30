using System.Windows.Controls;
using System.Windows.Input;
using eCheque.MICO360.ViewModels;
namespace eCheque.MICO360.Views
{
    public partial class UserManagementView : UserControl
    {
        public UserManagementView()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (DataContext is UserManagementViewModel vm) vm.Load();
            };
            // Sync PasswordBox.Password → ViewModel.NewPassword (PasswordBox cannot bind directly)
            PwdBox.PasswordChanged += (s, e) =>
            {
                if (DataContext is UserManagementViewModel vm) vm.NewPassword = PwdBox.Password;
            };
            // Clear password box when user selection changes
            DataContextChanged += (s, e) =>
            {
                if (e.NewValue is UserManagementViewModel vm2)
                {
                    vm2.PropertyChanged += (_, pe) =>
                    {
                        if (pe.PropertyName == nameof(UserManagementViewModel.Edit)) PwdBox.Clear();
                    };
                }
            };
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && DataContext is UserManagementViewModel vm && vm.SaveUserCommand.CanExecute(null))
                { vm.SaveUserCommand.Execute(null); e.Handled = true; }
            };
        }
    }
}
