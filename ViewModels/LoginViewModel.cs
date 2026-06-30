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
        Company? _selectedCompany;
        List<Company> _companies = new();

        public string Username { get => _username; set => Set(ref _username, value); }
        public string ErrorMessage { get => _error; set => Set(ref _error, value); }
        public bool IsLoading { get => _loading; set => Set(ref _loading, value); }
        public List<Company> Companies { get => _companies; set => Set(ref _companies, value); }
        public Company? SelectedCompany { get => _selectedCompany; set => Set(ref _selectedCompany, value); }

        public ICommand LoginCommand { get; }
        public event Action? LoginSuccessful;

        public LoginViewModel()
        {
            LoginCommand = new RelayCommand<string>(DoLogin);
            LoadCompanies();
        }

        public void LoadCompanies()
        {
            Companies = CompanyService.GetAll();
            SelectedCompany = Companies.Count > 0 ? Companies[0] : null;
        }

        public void DoLogin(string? password)
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            { ErrorMessage = "Please enter username and password."; return; }
            if (SelectedCompany == null)
            { ErrorMessage = "Please select a company."; return; }
            IsLoading = true;
            var dbPath = CompanyService.GetDbPath(SelectedCompany.Id);
            DatabaseProtectionService.DecryptOnStartup(dbPath);
            DatabaseService.Initialize(dbPath);
            CompanyService.SelectCompany(SelectedCompany.Id, SelectedCompany.Name);
            var error = AuthService.Login(Username.Trim(), password);
            if (error == null) LoginSuccessful?.Invoke();
            else ErrorMessage = error;
            IsLoading = false;
        }
    }
}
