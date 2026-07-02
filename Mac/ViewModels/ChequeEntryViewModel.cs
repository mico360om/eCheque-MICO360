using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class ChequeEntryViewModel : ViewModelBase
    {
        ChequeRecord _cheque = new();
        ChequeProfile? _selProfile;
        string _amountText = "", _status = "";

        public ObservableCollection<ChequeProfile> Profiles { get; } = new();
        public ObservableCollection<string> Payees { get; } = new();
        public ChequeRecord Cheque { get => _cheque; set => Set(ref _cheque, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }

        public ChequeProfile? SelectedProfile
        {
            get => _selProfile;
            set
            {
                Set(ref _selProfile, value);
                OnPropertyChanged(nameof(ProfileInfo));
                if (value != null)
                {
                    Cheque.ProfileId = value.Id; Cheque.ProfileName = value.Name;
                    Cheque.BankName = value.BankName; Cheque.AccountName = value.AccountName; Cheque.AccountNumber = value.AccountNumber;
                    if (string.IsNullOrWhiteSpace(Cheque.ChequeNumber))
                        Cheque.ChequeNumber = ChequeService.GetNextChequeNumber(value.Id);
                }
            }
        }

        public string ProfileInfo => _selProfile != null
            ? $"{_selProfile.BankName}  |  A/C {_selProfile.AccountNumber}  |  {_selProfile.ChequeWidth:N0} × {_selProfile.ChequeHeight:N0} mm"
            : "";

        // Amount entered as text so we can parse + auto-convert to words.
        public string AmountText
        {
            get => _amountText;
            set
            {
                Set(ref _amountText, value);
                if (decimal.TryParse(value?.Replace(",", "").Trim(), out var amt))
                {
                    Cheque.Amount = amt;
                    Cheque.AmountInWords = AmountToWordsService.Convert(amt);
                    OnPropertyChanged(nameof(Cheque));
                }
            }
        }

        public ICommand SaveDraftCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        public event Action? Saved;
        public event Action? Cancelled;

        public ChequeEntryViewModel()
        {
            SaveDraftCommand = new RelayCommand(SaveDraft);
            SaveCommand = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => Cancelled?.Invoke());
            Load();
        }

        public void Load()
        {
            Profiles.Clear();
            foreach (var p in ChequeService.GetProfiles()) Profiles.Add(p);
            Payees.Clear();
            foreach (var p in ChequeService.GetPayees()) Payees.Add(p);

            Cheque = new ChequeRecord
            {
                ChequeDate = DateTime.Today,
                Currency = DatabaseService.GetSetting("DefaultCurrency", "OMR"),
                Status = "Draft",
                CreatedBy = AuthService.CurrentUser?.Username ?? "",
                PreparedBy = AuthService.CurrentUser?.FullName ?? ""
            };
            AmountText = "";
            SelectedProfile = Profiles.FirstOrDefault();
            StatusMessage = "";
        }

        bool Validate()
        {
            if (!AuthService.CanEdit) { StatusMessage = "Your role is read-only and cannot create cheques."; return false; }
            if (SelectedProfile == null) { StatusMessage = "Please select a cheque profile."; return false; }
            if (string.IsNullOrWhiteSpace(Cheque.ChequeNumber)) { StatusMessage = "Cheque number is required."; return false; }
            if (string.IsNullOrWhiteSpace(Cheque.PayeeName)) { StatusMessage = "Payee name is required."; return false; }
            if (Cheque.Amount <= 0) { StatusMessage = "Amount must be greater than zero."; return false; }
            if (ChequeService.ChequeNumberExists(Cheque.ChequeNumber, Cheque.BankName, Cheque.Id))
            { StatusMessage = $"Cheque #{Cheque.ChequeNumber} already exists for {Cheque.BankName}."; return false; }
            return true;
        }

        void SaveDraft()
        {
            if (!AuthService.CanEdit) { StatusMessage = "Your role is read-only."; return; }
            if (string.IsNullOrWhiteSpace(Cheque.ChequeNumber)) { StatusMessage = "Cheque number is required."; return; }
            Cheque.AmountInWords = AmountToWordsService.Convert(Cheque.Amount);
            Cheque.Status = "Draft";
            ChequeService.SaveCheque(Cheque);
            StatusMessage = $"Draft saved — Cheque #{Cheque.ChequeNumber}";
        }

        void Save()
        {
            if (!Validate()) return;
            Cheque.AmountInWords = AmountToWordsService.Convert(Cheque.Amount);
            bool isNew = Cheque.Id == 0;
            Cheque.Status = "ReadyToPrint";
            ChequeService.SaveCheque(Cheque);
            if (isNew && SelectedProfile != null) ChequeService.IncrementChequeNumber(SelectedProfile.Id);
            StatusMessage = $"Cheque #{Cheque.ChequeNumber} saved.";
            Saved?.Invoke();
        }

        /// <summary>Validates + saves the cheque so the view can render/print it. Returns false (with a status
        /// message) if it can't proceed. Does NOT navigate away — the view opens the PDF afterwards.</summary>
        public bool PreparePrint()
        {
            if (!AuthService.CanEdit) { StatusMessage = "Your role is read-only."; return false; }
            if (SelectedProfile == null) { StatusMessage = "Please select a cheque profile."; return false; }
            if (!Validate()) return false;
            Cheque.AmountInWords = AmountToWordsService.Convert(Cheque.Amount);
            bool isNew = Cheque.Id == 0;
            Cheque.Status = "Printed";
            ChequeService.SaveCheque(Cheque);
            if (isNew) ChequeService.IncrementChequeNumber(SelectedProfile.Id);
            StatusMessage = $"Cheque #{Cheque.ChequeNumber} ready — opening a print PDF…";
            return true;
        }
    }
}
