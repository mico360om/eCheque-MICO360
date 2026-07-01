using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class LoginWindow : Window
    {
        readonly LoginViewModel _vm = new();

        public LoginWindow()
        {
            InitializeComponent();
            DataContext = _vm;
            _vm.LoginSuccessful += OnLoginSuccessful;
            TxtPassword.KeyDown += (s, e) => { if (e.Key == Key.Enter) DoLogin(); };
        }

        private void OnLoginClick(object? sender, RoutedEventArgs e) => DoLogin();

        private void DoLogin() => _vm.DoLogin(TxtPassword.Text);

        private void OnLoginSuccessful()
        {
            var main = new MainWindow();
            main.Show();
            Close();
        }
    }
}
