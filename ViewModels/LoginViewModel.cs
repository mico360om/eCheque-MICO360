using System.Collections.Generic;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        string _username = "", _error = ""; bool _loading;

        public string Username { get => _username; set => Set(ref _username, value); }
        public string ErrorMessage { get => _error; set => Set(ref _error, value); }
        public bool IsLoading { get => _loading; set => Set(ref _loading, value); }

        public ICommand LoginCommand { get; }
        public event Action? LoginSuccessful;

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand<string>(DoLogin);
        }

        // Single central login — no company selection. The user's role governs access; after
        // authentication the default company is opened and companies can be switched in-app.
        public void DoLogin(string? password)
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            { ErrorMessage = "Please enter username and password."; return; }

            IsLoading = true;
            try
            {
                var error = AuthService.Login(Username.Trim(), password);
                if (error != null) { ErrorMessage = error; return; }

                var company = CompanyService.GetAll().FirstOrDefault();
                if (company == null) { ErrorMessage = "No company is configured. Contact your administrator."; AuthService.Logout(); return; }

                CompanyService.OpenCompany(company.Id, company.Name);
                LoginSuccessful?.Invoke();
            }
            finally { IsLoading = false; }
        }
    }
}
