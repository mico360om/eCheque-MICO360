using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        // Stats
        int _total, _printed, _draft, _cancelled, _voided, _todayPrinted, _monthPrinted;
        decimal _totalAmount, _monthAmount, _yearAmount;
        string _companyName = "", _currency = "OMR", _backupStatus = "";
        int _pdcDue; string _pdcAlert = ""; bool _hasPdcAlert;
        ObservableCollection<ChequeRecord> _recent = new();

        public int PdcDue        { get => _pdcDue;      set => Set(ref _pdcDue, value); }
        public string PdcAlert   { get => _pdcAlert;    set => Set(ref _pdcAlert, value); }
        public bool HasPdcAlert  { get => _hasPdcAlert; set => Set(ref _hasPdcAlert, value); }

        public int Total          { get => _total;        set => Set(ref _total, value); }
        public int Printed        { get => _printed;      set => Set(ref _printed, value); }
        public int Draft          { get => _draft;        set => Set(ref _draft, value); }
        public int Cancelled      { get => _cancelled;    set => Set(ref _cancelled, value); }
        public int Voided         { get => _voided;       set => Set(ref _voided, value); }
        public int TodayPrinted   { get => _todayPrinted; set => Set(ref _todayPrinted, value); }
        public int MonthPrinted   { get => _monthPrinted; set => Set(ref _monthPrinted, value); }
        public decimal TotalAmount { get => _totalAmount; set => Set(ref _totalAmount, value); }
        public decimal MonthAmount { get => _monthAmount; set => Set(ref _monthAmount, value); }
        public decimal YearAmount  { get => _yearAmount;  set => Set(ref _yearAmount, value); }
        public string CompanyName  { get => _companyName; set => Set(ref _companyName, value); }
        public string Currency     { get => _currency;    set => Set(ref _currency, value); }
        public string BackupStatus { get => _backupStatus; set => Set(ref _backupStatus, value); }
        public ObservableCollection<ChequeRecord> Recent { get => _recent; set => Set(ref _recent, value); }

        // Navigation events wired by MainWindow
        public event Action? NewChequeRequested;
        public event Action? HistoryRequested;
        public event Action? PendingChequesRequested;
        public event Action? PdcRequested;
        public event Action<ChequeRecord, ChequeProfile>? PrintRequested;

        public ICommand NewChequeCommand     { get; }
        public ICommand HistoryCommand       { get; }
        public ICommand PendingCommand       { get; }
        public ICommand BackupCommand        { get; }
        public ICommand RefreshCommand       { get; }
        public ICommand PrintRowCommand      { get; }
        public ICommand ViewRowCommand       { get; }
        public ICommand PdcCommand           { get; }

        public DashboardViewModel()
        {
            NewChequeCommand  = new RelayCommand(() => NewChequeRequested?.Invoke());
            HistoryCommand    = new RelayCommand(() => HistoryRequested?.Invoke());
            PendingCommand    = new RelayCommand(() => PendingChequesRequested?.Invoke());
            PdcCommand        = new RelayCommand(() => PdcRequested?.Invoke());
            BackupCommand     = new RelayCommand(DoBackup);
            RefreshCommand    = new RelayCommand(Load);
            PrintRowCommand   = new RelayCommand<ChequeRecord>(DoPrint, r => r != null);
            ViewRowCommand    = new RelayCommand<ChequeRecord>(r => { if (r != null) HistoryRequested?.Invoke(); });
        }

        public void Load()
        {
            try
            {
                CompanyName = CompanyService.CurrentCompanyName.Length > 0
                    ? CompanyService.CurrentCompanyName
                    : DatabaseService.GetSetting("CompanyName", "My Company");
                Currency = DatabaseService.GetSetting("DefaultCurrency", "OMR");

                var s = ChequeService.GetDashboardStats();
                Total        = s.Total;
                Printed      = s.Printed;
                Draft        = s.Draft;
                Cancelled    = s.Cancelled;
                Voided       = s.Voided;
                TodayPrinted = s.TodayPrinted;
                MonthPrinted = s.MonthPrinted;
                TotalAmount  = s.TotalAmount;
                MonthAmount  = s.MonthAmount;
                YearAmount   = s.YearAmount;

                Recent = new ObservableCollection<ChequeRecord>(ChequeService.GetCheques(limit: 10));

                // Post-dated cheque reminder
                var reminderDays = int.TryParse(DatabaseService.GetSetting("PdcReminderDays", "7"), out var d) ? d : 7;
                PdcDue = ChequeService.GetDuePdcCount(reminderDays);
                HasPdcAlert = PdcDue > 0;
                PdcAlert = PdcDue > 0
                    ? $"⏰  {PdcDue} post-dated cheque(s) due within {reminderDays} days — click to review."
                    : "";
            }
            catch { }
        }

        void DoPrint(ChequeRecord? r)
        {
            if (r == null) return;
            var profile = ChequeService.GetProfile(r.ProfileId);
            if (profile != null) PrintRequested?.Invoke(r, profile);
        }

        void DoBackup()
        {
            try
            {
                var path = BackupService.CreateBackup();
                BackupStatus = $"Backup saved: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                BackupStatus = $"Backup failed: {ex.Message}";
            }
        }
    }
}
