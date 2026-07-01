using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        string _username = "", _error = "";

        public string Username { get => _username; set => Set(ref _username, value); }
        public string ErrorMessage { get => _error; set => Set(ref _error, value); }

        public ICommand LoginCommand { get; }
        public event Action? LoginSuccessful;

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand<object?>(p => DoLogin(p as string));
        }

        // Single central login — no company selection. The default company opens after auth;
        // companies are switched in-app based on the user's access level.
        public void DoLogin(string? password)
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(Username)) { ErrorMessage = "Please enter your username."; return; }

            var error = AuthService.Login(Username.Trim(), password ?? "");
            if (error != null) { ErrorMessage = error; return; }

            var company = CompanyService.GetAll().FirstOrDefault();
            if (company == null) { ErrorMessage = "No company is configured. Contact your administrator."; AuthService.Logout(); return; }

            CompanyService.OpenCompany(company.Id, company.Name);
            LoginSuccessful?.Invoke();
        }
    }
}
