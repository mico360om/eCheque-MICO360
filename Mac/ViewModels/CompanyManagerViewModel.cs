using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    /// <summary>Company Manager — list companies and edit their details (mirrors the Windows screen).</summary>
    public class CompanyManagerViewModel : ViewModelBase
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
                if (!Set(ref _selected, value)) return;
                // Selecting a company shows its details (read-only) and cancels any in-progress edit.
                if (value != null) Edit = Clone(value);
                IsEditing = false;
                StatusMessage = "";
                (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public Company Edit { get => _edit; set => Set(ref _edit, value); }

        public bool IsEditing
        {
            get => _isEditing;
            set { if (Set(ref _isEditing, value)) { OnPropertyChanged(nameof(IsViewing)); (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
        }
        public bool IsViewing => !_isEditing;

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

        void CancelEdit() { Edit = Selected != null ? Clone(Selected) : new Company { Currency = "OMR" }; IsEditing = false; StatusMessage = ""; }

        void SaveCompany()
        {
            if (string.IsNullOrWhiteSpace(Edit.Name)) { StatusMessage = "Company name is required."; return; }
            CompanyService.Save(Edit);
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", Edit.Id == 0 ? "Company Created" : "Company Updated", Edit.Name);
            Load();
            IsEditing = false;
            StatusMessage = "Company saved.";
            CompanyListChanged?.Invoke();
        }

        void DeleteCompany()
        {
            if (Selected == null) return;
            if (Selected.Id == CompanyService.CurrentCompanyId) { StatusMessage = "Cannot delete the active company."; return; }
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Company Deleted", Selected.Name);
            CompanyService.Delete(Selected.Id);
            Load();
            StatusMessage = "Company deleted.";
            CompanyListChanged?.Invoke();
        }

        static Company Clone(Company c) => new() { Id = c.Id, Name = c.Name, TradeName = c.TradeName, Address = c.Address, Phone = c.Phone, Email = c.Email, Currency = c.Currency, CreatedDate = c.CreatedDate, IsActive = c.IsActive };
    }
}
