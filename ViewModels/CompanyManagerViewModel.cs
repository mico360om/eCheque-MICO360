using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public class CompanyManagerViewModel : BaseViewModel
    {
        ObservableCollection<Company> _companies = new();
        Company? _selected;
        Company _edit = new();
        bool _isEditing;
        string _status = "";

        public ObservableCollection<Company> Companies { get => _companies; set => Set(ref _companies, value); }
        public Company? Selected
        {
            get => _selected;
            set
            {
                Set(ref _selected, value);
                // Selecting a company always shows its details (read-only) and cancels any in-progress edit.
                if (value != null) Edit = Clone(value);
                IsEditing = false;
                StatusMessage = "";
            }
        }
        public Company Edit { get => _edit; set => Set(ref _edit, value); }
        public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public List<string> Currencies { get; } = new() { "OMR", "USD", "EUR", "GBP", "AED", "SAR", "QAR", "KWD", "BHD" };

        public ICommand NewCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand CancelEditCommand { get; }
        public event Action? CompanyListChanged;

        public CompanyManagerViewModel()
        {
            NewCommand = new RelayCommand(NewCompany);
            EditCommand = new RelayCommand(EditCompany, () => Selected != null);
            SaveCommand = new RelayCommand(SaveCompany, () => IsEditing);
            DeleteCommand = new RelayCommand(DeleteCompany, () => Selected != null && Selected.Id != CompanyService.CurrentCompanyId);
            CancelEditCommand = new RelayCommand(CancelEdit);
        }

        public void Load()
        {
            Companies = new ObservableCollection<Company>(CompanyService.GetAll());
            Selected = Companies.FirstOrDefault(c => c.Id == CompanyService.CurrentCompanyId) ?? Companies.FirstOrDefault();
        }

        void NewCompany() { Edit = new Company { Currency = "OMR" }; IsEditing = true; StatusMessage = ""; }
        void EditCompany() { if (Selected == null) return; Edit = Clone(Selected); IsEditing = true; StatusMessage = ""; }
        void CancelEdit() { if (Selected != null) Edit = Clone(Selected); IsEditing = false; StatusMessage = ""; }

        void SaveCompany()
        {
            if (string.IsNullOrWhiteSpace(Edit.Name)) { StatusMessage = "Company name is required."; return; }
            CompanyService.Save(Edit);
            Load();
            IsEditing = false;
            StatusMessage = "Company saved.";
            CompanyListChanged?.Invoke();
        }

        void DeleteCompany()
        {
            if (Selected == null) return;
            if (Selected.Id == CompanyService.CurrentCompanyId) { StatusMessage = "Cannot delete the active company."; return; }
            CompanyService.Delete(Selected.Id);
            Load();
            StatusMessage = "Company deleted.";
            CompanyListChanged?.Invoke();
        }

        Company Clone(Company c) => new() { Id = c.Id, Name = c.Name, TradeName = c.TradeName, Address = c.Address, Phone = c.Phone, Email = c.Email, Currency = c.Currency };
    }
}
