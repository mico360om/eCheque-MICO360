using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    /// <summary>Cheque Profile Manager — list profiles and edit their print layout details (mirrors the Windows screen).</summary>
    public class ProfileManagerViewModel : ViewModelBase
    {
        ObservableCollection<ChequeProfile> _profiles = new();
        ChequeProfile? _selected;
        ChequeProfile _edit = new();
        bool _isEditing;
        string _status = "";
        int _defaultId;

        /// <summary>Id of the profile New Cheque pre-selects.</summary>
        public int DefaultProfileId { get => _defaultId; set { if (Set(ref _defaultId, value)) { OnPropertyChanged(nameof(SelectedIsDefault)); (SetDefaultCommand as RelayCommand)?.RaiseCanExecuteChanged(); } } }
        public bool SelectedIsDefault => Selected != null && Selected.Id == _defaultId && _defaultId != 0;

        public ObservableCollection<ChequeProfile> Profiles { get => _profiles; set => Set(ref _profiles, value); }

        public ChequeProfile? Selected
        {
            get => _selected;
            set
            {
                if (!Set(ref _selected, value)) return;
                // Selecting a profile shows its details (read-only) and cancels any in-progress edit.
                if (value != null) Edit = Clone(value);
                IsEditing = false;
                StatusMessage = "";
                OnPropertyChanged(nameof(SelectedIsDefault));
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DuplicateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DesignLayoutCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SetDefaultCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public ChequeProfile Edit { get => _edit; set => Set(ref _edit, value); }

        public bool IsEditing
        {
            get => _isEditing;
            set { if (Set(ref _isEditing, value)) { OnPropertyChanged(nameof(IsViewing)); (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged(); } }
        }
        public bool IsViewing => !_isEditing;

        public string StatusMessage { get => _status; set => Set(ref _status, value); }

        public List<string> FontFamilies { get; } = new() { "Arial", "Times New Roman", "Calibri", "Courier New", "Verdana", "Tahoma" };
        public List<string> PaperSizes { get; } = new() { "A4", "Letter", "Legal", "A5" };
        public List<string> Banks { get; } = new();

        public ICommand NewCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand DuplicateCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand DesignLayoutCommand { get; }
        public ICommand SetDefaultCommand { get; }

        public event Action? ProfileListChanged;

        public ProfileManagerViewModel()
        {
            NewCommand = new RelayCommand(NewProfile);
            SaveCommand = new RelayCommand(SaveProfile, () => IsEditing);
            DeleteCommand = new RelayCommand(DeleteProfile, () => Selected != null);
            DuplicateCommand = new RelayCommand(Duplicate, () => Selected != null);
            CancelEditCommand = new RelayCommand(CancelEdit);
            DesignLayoutCommand = new RelayCommand(OpenDesigner, () => Selected != null);
            SetDefaultCommand = new RelayCommand(SetDefault, () => Selected != null && Selected.Id != 0 && Selected.Id != DefaultProfileId);
        }

        void SetDefault()
        {
            if (Selected == null || Selected.Id == 0) return;
            ChequeService.SetDefaultProfile(Selected.Id);
            DefaultProfileId = Selected.Id;
            StatusMessage = $"'{Selected.Name}' is now the default profile for new cheques.";
        }

        public void Load()
        {
            var keepId = Selected?.Id ?? 0;
            Profiles = new ObservableCollection<ChequeProfile>(ChequeService.GetProfiles(false));
            Banks.Clear();
            foreach (var b in ChequeService.GetBanks()) Banks.Add(b);
            OnPropertyChanged(nameof(Banks));
            DefaultProfileId = ChequeService.GetDefaultProfileId();
            Selected = keepId > 0 ? Profiles.FirstOrDefault(p => p.Id == keepId) ?? Profiles.FirstOrDefault() : Profiles.FirstOrDefault();
        }

        void NewProfile()
        {
            Edit = new ChequeProfile { CreatedBy = AuthService.CurrentUser?.Username ?? "" };
            IsEditing = true;
            StatusMessage = "";
        }

        void SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(Edit.Name)) { StatusMessage = "Profile name is required."; return; }
            ChequeService.SaveProfile(Edit);
            Load();
            Selected = Profiles.FirstOrDefault(p => p.Id == Edit.Id);
            IsEditing = false;
            StatusMessage = "Profile saved.";
            ProfileListChanged?.Invoke();
        }

        void DeleteProfile()
        {
            if (Selected == null) return;
            int used = ChequeService.CountChequesUsingProfile(Selected.Id);
            if (used > 0) { StatusMessage = $"Cannot delete — this profile is used by {used} cheque(s)."; return; }
            if (ChequeService.DeleteProfile(Selected.Id))
            {
                Load();
                StatusMessage = "Profile deleted.";
                ProfileListChanged?.Invoke();
            }
            else StatusMessage = "Profile is in use and cannot be deleted.";
        }

        void Duplicate()
        {
            if (Selected == null) return;
            var c = Clone(Selected);
            c.Id = 0;
            c.Name += " (Copy)";
            c.CreatedBy = AuthService.CurrentUser?.Username ?? "";
            Edit = c;
            IsEditing = true;
            StatusMessage = "Duplicated — the field layout was copied. Click Save Profile.";
        }

        void CancelEdit()
        {
            Edit = Selected != null ? Clone(Selected) : new ChequeProfile();
            IsEditing = false;
            StatusMessage = "";
        }

        public event Action<ChequeProfile>? DesignLayoutRequested;

        void OpenDesigner()
        {
            if (Selected == null) { StatusMessage = "Select a profile first."; return; }
            DesignLayoutRequested?.Invoke(Selected);
        }

        static ChequeProfile Clone(ChequeProfile p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            BankName = p.BankName,
            AccountName = p.AccountName,
            AccountNumber = p.AccountNumber,
            ChequeWidth = p.ChequeWidth,
            ChequeHeight = p.ChequeHeight,
            DateX = p.DateX,
            DateY = p.DateY,
            PayeeX = p.PayeeX,
            PayeeY = p.PayeeY,
            AmountNumX = p.AmountNumX,
            AmountNumY = p.AmountNumY,
            AmountWordsX = p.AmountWordsX,
            AmountWordsY = p.AmountWordsY,
            ChequeNumX = p.ChequeNumX,
            ChequeNumY = p.ChequeNumY,
            FontFamily = p.FontFamily,
            FontSize = p.FontSize,
            IsBold = p.IsBold,
            PrintOffsetX = p.PrintOffsetX,
            PrintOffsetY = p.PrintOffsetY,
            PaperSize = p.PaperSize,
            IsActive = p.IsActive,
            CreatedDate = p.CreatedDate,
            CreatedBy = p.CreatedBy,
            LastChequeNumber = p.LastChequeNumber
        };
    }
}
