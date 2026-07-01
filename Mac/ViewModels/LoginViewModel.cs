using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        string _username = "", _error = "";
        Company? _selectedCompany;

        public ObservableCollection<Company> Companies { get; } = new();
        public Company? SelectedCompany { get => _selectedCompany; set => Set(ref _selectedCompany, value); }
        public string Username { get => _username; set => Set(ref _username, value); }
        public string ErrorMessage { get => _error; set => Set(ref _error, value); }

        public ICommand LoginCommand { get; }
        public event Action? LoginSuccessful;

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand<object?>(p => DoLogin(p as string));
            foreach (var c in CompanyService.GetAll()) Companies.Add(c);
            SelectedCompany = Companies.FirstOrDefault();
        }

        // The password comes from the PasswordBox via the command parameter (not data-bound for safety).
        public void DoLogin(string? password)
        {
            ErrorMessage = "";
            if (SelectedCompany == null) { ErrorMessage = "Please select a company."; return; }
            if (string.IsNullOrWhiteSpace(Username)) { ErrorMessage = "Please enter your username."; return; }

            var dbPath = CompanyService.GetDbPath(SelectedCompany.Id);
            DatabaseService.Initialize(dbPath);
            CompanyService.SelectCompany(SelectedCompany.Id, SelectedCompany.Name);

            var error = AuthService.Login(Username.Trim(), password ?? "");
            if (error == null) LoginSuccessful?.Invoke();
            else ErrorMessage = error;
        }
    }
}
