using System.Windows;
using System.Windows.Input;
using eCheque.MICO360.ViewModels;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace eCheque.MICO360.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            var vm = (LoginViewModel)DataContext;
            vm.LoginSuccessful += () =>
            {
                var main = new MainWindow();
                main.Show();
                Close();
            };
            TxtPassword.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) vm.DoLogin(TxtPassword.Password);
            };
            TxtUsername.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) TxtPassword.Focus();
            };

            // Highlight border on focus
            TxtUsername.GotFocus  += (s, e) => UsernameBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x18, 0x18));
            TxtUsername.LostFocus += (s, e) => UsernameBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
            TxtPassword.GotFocus  += (s, e) => PasswordBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x18, 0x18));
            TxtPassword.LostFocus += (s, e) => PasswordBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
        }

        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            ((LoginViewModel)DataContext).DoLogin(TxtPassword.Password);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
