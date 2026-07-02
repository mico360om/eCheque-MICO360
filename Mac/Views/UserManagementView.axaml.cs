using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class UserManagementView : UserControl
    {
        public UserManagementView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        void OnDataContextChanged(object? sender, System.EventArgs e)
        {
            if (DataContext is UserManagementViewModel vm)
                vm.PropertyChanged += OnVmPropertyChanged;
        }

        // Clear the (unbound) password box whenever the edited user changes.
        void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserManagementViewModel.Edit))
                PwdBox.Text = "";
        }

        void OnSaveUser(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not UserManagementViewModel vm) return;
            vm.NewPassword = PwdBox.Text ?? "";
            vm.SaveUserCommand.Execute(null);
        }
    }
}
