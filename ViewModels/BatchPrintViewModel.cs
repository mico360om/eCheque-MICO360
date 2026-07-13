using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    /// <summary>
    /// Batch entry &amp; printing: import a list of payees + amounts from Excel/CSV, auto-assign sequential leaf
    /// numbers from the account's cheque book, validate every row (amount, duplicate, range, spoiled), then save
    /// as drafts or print the whole run in one pass. The actual printing is delegated to the window (needs the
    /// print dialog + UI thread) via <see cref="PrintBatchRequested"/>.
    /// </summary>
    public class BatchPrintViewModel : BaseViewModel
    {
        ObservableCollection<ChequeProfile> _profiles = new();
        ObservableCollection<BatchRow> _rows = new();
        ChequeProfile? _profile;
        string _startOverride = "";
        string _status = "";

        public ObservableCollection<ChequeProfile> Profiles { get => _profiles; set => Set(ref _profiles, value); }
        public ObservableCollection<BatchRow> Rows { get => _rows; set { Set(ref _rows, value); OnPropertyChanged(nameof(Summary)); OnPropertyChanged(nameof(IsEmpty)); } }
        public bool IsEmpty => _rows.Count == 0;
        public string StartNumberOverride { get => _startOverride; set => Set(ref _startOverride, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public bool CanEdit => AuthService.CanEdit;

        public ChequeProfile? SelectedProfile
        {
            get => _profile;
            set { Set(ref _profile, value); OnPropertyChanged(nameof(ProfileInfo)); if (Rows.Count > 0) { AssignNumbers(); Validate(); } }
        }

        public string ProfileInfo => _profile == null ? "Select the account/profile these cheques print on."
            : $"{_profile.BankName}   ·   Account {_profile.AccountNumber}   ·   {ChequeBookNote()}";

        public string Summary
        {
            get
            {
                if (Rows.Count == 0) return "";
                int ok = Rows.Count(r => r.IsValid);
                decimal total = Rows.Where(r => r.IsValid).Sum(r => r.Amount);
                var cur = DatabaseService.GetSetting("DefaultCurrency", "OMR");
                return $"{Rows.Count} row(s)   ·   {ok} ready   ·   {Rows.Count - ok} with issues   ·   Total {cur} {total:N3}";
            }
        }

        public ICommand ImportCommand { get; }
        public ICommand ValidateCommand { get; }
        public ICommand AssignCommand { get; }
        public ICommand SaveDraftsCommand { get; }
        public ICommand PrintAllCommand { get; }
        public ICommand ClearCommand { get; }

        /// <summary>Raised to print the built, already-saved cheques (window owns the print dialog + status update).</summary>
        public event Action<List<ChequeRecord>, ChequeProfile>? PrintBatchRequested;

        public BatchPrintViewModel()
        {
            ImportCommand     = new RelayCommand(Import, () => CanEdit);
            ValidateCommand   = new RelayCommand(() => { Validate(); }, () => Rows.Count > 0);
            AssignCommand     = new RelayCommand(() => { AssignNumbers(); Validate(); }, () => Rows.Count > 0 && SelectedProfile != null);
            SaveDraftsCommand = new RelayCommand(SaveDrafts, () => CanEdit && AnyValid());
            PrintAllCommand   = new RelayCommand(PrintAll, () => CanEdit && AnyValid());
            ClearCommand      = new RelayCommand(() => { Rows = new(); StatusMessage = ""; });
        }

        public void Load()
        {
            Profiles = new ObservableCollection<ChequeProfile>(ChequeService.GetProfiles());
            var defId = ChequeService.GetDefaultProfileId();
            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == defId) ?? Profiles.FirstOrDefault();
        }

        bool AnyValid() => Rows.Any(r => r.IsValid);

        string ChequeBookNote()
        {
            if (_profile == null) return "";
            var book = ChequeBookService.ActiveBookFor(_profile.BankName, _profile.AccountNumber);
            if (book == null) return "no cheque book (numbers auto-increment)";
            var s = ChequeBookService.Stats(book);
            return s.NextNumber is int n ? $"next leaf {book.Format(n)} ({s.Remaining} left)" : "cheque book fully used";
        }

        void Import()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import batch (Excel or CSV)",
                Filter = "Spreadsheet (*.xlsx;*.csv)|*.xlsx;*.csv|Excel (*.xlsx)|*.xlsx|CSV (*.csv)|*.csv"
            };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var rows = BatchImportService.Parse(dlg.FileName);
                if (rows.Count == 0) { StatusMessage = "No rows found in that file."; return; }
                Rows = new ObservableCollection<BatchRow>(rows);
                AssignNumbers();
                Validate();
                StatusMessage = $"Imported {rows.Count} row(s) from {System.IO.Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex) { StatusMessage = $"Import failed: {ex.Message}"; ToastService.Warn("Import failed."); }
        }

        /// <summary>Fills the ChequeNumber of every row that doesn't already have one, drawing sequential unused
        /// leaves from the account's active cheque book (skipping used + spoiled), or a plain running number.</summary>
        void AssignNumbers()
        {
            if (_profile == null) return;
            var book = ChequeBookService.ActiveBookFor(_profile.BankName, _profile.AccountNumber);
            int? overrideStart = int.TryParse(StartNumberOverride?.Trim(), out var os) ? os : (int?)null;

            if (book != null)
            {
                var stats = ChequeBookService.Stats(book);
                var taken = new HashSet<int>(stats.UsedNumbers);
                foreach (var sp in book.SpoiledNumbers()) taken.Add(sp);
                int n = overrideStart ?? stats.NextNumber ?? book.StartNumber;
                foreach (var row in Rows)
                {
                    if (!string.IsNullOrWhiteSpace(row.ChequeNumber)) continue;
                    while (n <= book.EndNumber && taken.Contains(n)) n++;
                    if (n > book.EndNumber) { row.ChequeNumber = ""; continue; } // book exhausted — flagged in Validate
                    row.ChequeNumber = book.Format(n);
                    taken.Add(n); n++;
                }
            }
            else
            {
                int n = overrideStart ?? 1;
                foreach (var row in Rows)
                {
                    if (!string.IsNullOrWhiteSpace(row.ChequeNumber)) continue;
                    row.ChequeNumber = n.ToString("D6"); n++;
                }
            }
            OnPropertyChanged(nameof(ProfileInfo));
        }

        void Validate()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in Rows)
            {
                string err = "";
                if (string.IsNullOrWhiteSpace(row.PayeeName)) err = "Payee is required.";
                else if (row.Amount <= 0) err = "Amount must be greater than zero.";
                else if (string.IsNullOrWhiteSpace(row.ChequeNumber)) err = "No cheque number (book may be exhausted).";
                else if (!seen.Add(row.ChequeNumber)) err = "Duplicate cheque number within this batch.";
                else if (_profile != null)
                {
                    var (res, msg) = ChequeBookService.Validate(_profile.BankName, _profile.AccountNumber, row.ChequeNumber);
                    if (res != ChequeBookService.LeafCheck.Ok && res != ChequeBookService.LeafCheck.NoBook) err = msg;
                }
                row.Error = err;
            }
            OnPropertyChanged(nameof(Summary));
        }

        List<ChequeRecord> BuildValid()
        {
            var cur = DatabaseService.GetSetting("DefaultCurrency", "OMR");
            var list = new List<ChequeRecord>();
            foreach (var row in Rows.Where(r => r.IsValid))
            {
                list.Add(new ChequeRecord
                {
                    ChequeNumber = row.ChequeNumber,
                    ChequeDate = row.ChequeDate,
                    PayeeName = row.PayeeName,
                    Amount = row.Amount,
                    AmountInWords = AmountToWordsService.Convert(row.Amount),
                    BankName = _profile!.BankName,
                    AccountName = _profile.AccountName,
                    AccountNumber = _profile.AccountNumber,
                    ProfileId = _profile.Id,
                    ProfileName = _profile.Name,
                    Currency = cur,
                    ReferenceNumber = row.Reference,
                    InvoiceNumber = row.Invoice,
                    Remarks = row.Memo,
                    PreparedBy = AuthService.CurrentUser?.FullName ?? "",
                    CreatedBy = AuthService.CurrentUser?.Username ?? "",
                    Status = "ReadyToPrint",
                    CreatedDate = DateTime.Now
                });
            }
            return list;
        }

        void SaveDrafts()
        {
            if (_profile == null) { StatusMessage = "Select a profile first."; return; }
            Validate();
            var records = BuildValid();
            if (records.Count == 0) { StatusMessage = "No valid rows to save."; return; }
            int n = 0;
            foreach (var c in records) { c.Status = "Draft"; ChequeService.SaveCheque(c); n++; }
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Batch Drafts Saved", $"{n} cheque(s)");
            StatusMessage = $"Saved {n} draft cheque(s).";
            ToastService.Success($"Saved {n} draft(s).");
        }

        void PrintAll()
        {
            if (_profile == null) { StatusMessage = "Select a profile first."; return; }
            Validate();
            var records = BuildValid();
            if (records.Count == 0) { StatusMessage = "No valid rows to print."; return; }
            // Persist first (as ReadyToPrint) so each gets an Id; the window prints them and marks them Printed.
            foreach (var c in records) ChequeService.SaveCheque(c);
            PrintBatchRequested?.Invoke(records, _profile);
        }
    }
}
