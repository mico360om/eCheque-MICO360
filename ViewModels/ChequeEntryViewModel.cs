using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public class ChequeEntryViewModel : BaseViewModel
    {
        ChequeRecord _cheque = new();
        ObservableCollection<ChequeProfile> _profiles = new();
        ObservableCollection<string> _banks = new();
        ObservableCollection<string> _payees = new();
        ChequeProfile? _selProfile;
        string _status = "";
        bool _isEdit;

        public ChequeRecord Cheque { get => _cheque; set { Set(ref _cheque, value); OnPropertyChanged(nameof(ProfileInfo)); } }
        public ObservableCollection<ChequeProfile> Profiles { get => _profiles; set => Set(ref _profiles, value); }
        public ObservableCollection<string> Banks    { get => _banks;   set => Set(ref _banks, value); }
        public ObservableCollection<string> Payees   { get => _payees;  set => Set(ref _payees, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public bool IsEdit { get => _isEdit; set => Set(ref _isEdit, value); }

        public ChequeProfile? SelectedProfile
        {
            get => _selProfile;
            set
            {
                Set(ref _selProfile, value);
                OnPropertyChanged(nameof(ProfileInfo));
                OnPropertyChanged(nameof(HasProfile));
                if (value != null)
                {
                    Cheque.ProfileId   = value.Id;
                    Cheque.ProfileName = value.Name;
                    if (!IsEdit)
                    {
                        Cheque.BankName      = value.BankName;
                        Cheque.AccountName   = value.AccountName;
                        Cheque.AccountNumber = value.AccountNumber;
                        if (string.IsNullOrWhiteSpace(Cheque.ChequeNumber))
                        {
                            // Prefer the next unused leaf from the account's cheque book; fall back to the
                            // profile's running counter when no book is defined.
                            var next = ChequeBookService.NextLeaf(value.BankName, value.AccountNumber);
                            Cheque.ChequeNumber = !string.IsNullOrEmpty(next) ? next : ChequeService.GetNextChequeNumber(value.Id);
                        }
                    }
                    OnPropertyChanged(nameof(Cheque));
                }
            }
        }

        public string ProfileInfo => _selProfile != null
            ? $"{_selProfile.BankName}   |   Account: {_selProfile.AccountNumber}   |   Cheque size: {_selProfile.ChequeWidth:N0} × {_selProfile.ChequeHeight:N0} mm   |   Font: {_selProfile.FontFamily} {_selProfile.FontSize}pt"
            : "";
        public bool HasProfile => _selProfile != null;

        public ICommand SaveCommand        { get; }
        public ICommand SaveDraftCommand   { get; }
        public ICommand PreviewCommand     { get; }
        public ICommand SavePrintCommand   { get; }
        public ICommand ConvertWordsCommand{ get; }
        public ICommand ClearCommand       { get; }
        public ICommand CancelCommand      { get; }

        public event Action<ChequeRecord, ChequeProfile>? PrintRequested;
        public event Action<ChequeRecord, ChequeProfile>? PreviewRequested;
        public event Action? Saved;
        public event Action? Cancelled;

        public ChequeEntryViewModel()
        {
            SaveCommand         = new RelayCommand(Save);
            SaveDraftCommand    = new RelayCommand(SaveDraft);
            PreviewCommand      = new RelayCommand(DoPreview);
            SavePrintCommand    = new RelayCommand(SaveAndPrint);
            ConvertWordsCommand = new RelayCommand(ConvertWords);
            ClearCommand        = new RelayCommand(() => Load());
            CancelCommand       = new RelayCommand(() => Cancelled?.Invoke());
        }

        public void Load(int? id = null)
        {
            Profiles = new ObservableCollection<ChequeProfile>(ChequeService.GetProfiles());
            Banks    = new ObservableCollection<string>(ChequeService.GetBanks());
            Payees   = new ObservableCollection<string>(ChequeService.GetPayees());

            if (id.HasValue)
            {
                var c = ChequeService.GetCheque(id.Value);
                if (c != null)
                {
                    Cheque = c; IsEdit = true;
                    SelectedProfile = Profiles.FirstOrDefault(p => p.Id == c.ProfileId);
                }
            }
            else
            {
                IsEdit = false;
                var currency = DatabaseService.GetSetting("DefaultCurrency", "OMR");
                Cheque = new ChequeRecord
                {
                    ChequeDate  = DateTime.Today,
                    Currency    = currency,
                    Status      = "Draft",
                    CreatedBy   = AuthService.CurrentUser?.Username ?? "",
                    PreparedBy  = AuthService.CurrentUser?.FullName ?? ""
                };
                // Pre-select the default cheque profile (fills bank/account + next cheque number).
                var defId = ChequeService.GetDefaultProfileId();
                if (defId > 0) SelectedProfile = Profiles.FirstOrDefault(p => p.Id == defId);
            }
            StatusMessage = "";
        }

        public void ConvertWords()
        {
            if (Cheque.Amount <= 0) return;
            Cheque.AmountInWords = AmountToWordsService.Convert(Cheque.Amount);
        }

        bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Cheque.ChequeNumber)) { StatusMessage = "Cheque number is required."; return false; }
            if (string.IsNullOrWhiteSpace(Cheque.PayeeName))    { StatusMessage = "Payee name is required."; return false; }
            if (Cheque.Amount <= 0)                              { StatusMessage = "Amount must be greater than zero."; return false; }
            if (SelectedProfile == null)                         { StatusMessage = "Please select a cheque profile."; return false; }
            if (ChequeService.ChequeNumberExists(Cheque.ChequeNumber, Cheque.BankName, Cheque.Id))
            { StatusMessage = $"Cheque #{Cheque.ChequeNumber} already exists for {Cheque.BankName}."; return false; }
            // Cheque-book inventory guard: block out-of-range or spoiled leaves when a book is defined for this account.
            var (leaf, leafMsg) = ChequeBookService.Validate(Cheque.BankName, Cheque.AccountNumber, Cheque.ChequeNumber, Cheque.Id);
            if (leaf != ChequeBookService.LeafCheck.Ok && leaf != ChequeBookService.LeafCheck.NoBook)
            { StatusMessage = leafMsg; return false; }
            if (string.IsNullOrWhiteSpace(Cheque.AmountInWords)) ConvertWords();
            StatusMessage = "";
            return true;
        }

        void SaveDraft()
        {
            if (string.IsNullOrWhiteSpace(Cheque.ChequeNumber)) { StatusMessage = "Cheque number is required."; return; }
            ConvertWords();
            Cheque.Status = "Draft";
            ChequeService.SaveCheque(Cheque);
            StatusMessage = $"Draft saved — Cheque #{Cheque.ChequeNumber}";
            ToastService.Success($"Draft saved — Cheque #{Cheque.ChequeNumber}");
        }

        void Save()
        {
            if (!Validate()) return;
            ConvertWords();
            bool isNew = Cheque.Id == 0;
            Cheque.Status = "ReadyToPrint";
            ChequeService.SaveCheque(Cheque);
            if (isNew && SelectedProfile != null) ChequeService.IncrementChequeNumber(SelectedProfile.Id);
            StatusMessage = $"Cheque #{Cheque.ChequeNumber} saved.";
            ToastService.Success($"Cheque #{Cheque.ChequeNumber} saved.");
            Saved?.Invoke();
        }

        void DoPreview()
        {
            if (!Validate()) return;
            ConvertWords();
            // Preserve the existing status — never downgrade a printed/issued cheque back to Draft.
            if (string.IsNullOrWhiteSpace(Cheque.Status) || Cheque.Status == "ReadyToPrint")
                Cheque.Status = "Draft";
            ChequeService.SaveCheque(Cheque);
            if (SelectedProfile != null) PreviewRequested?.Invoke(Cheque, SelectedProfile);
        }

        void SaveAndPrint()
        {
            if (!Validate()) return;
            ConvertWords();
            bool isNew = Cheque.Id == 0;
            Cheque.Status = "ReadyToPrint";
            ChequeService.SaveCheque(Cheque);
            if (isNew && SelectedProfile != null) ChequeService.IncrementChequeNumber(SelectedProfile.Id);
            if (SelectedProfile != null) PrintRequested?.Invoke(Cheque, SelectedProfile);
        }
    }
}
