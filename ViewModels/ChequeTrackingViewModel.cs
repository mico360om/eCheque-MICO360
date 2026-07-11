using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    /// <summary>
    /// Powers the Cheque Tracking screen with two modes:
    ///  • PDC Register   — post-dated cheques ordered by due date
    ///  • Reconciliation — issued cheques awaiting Present/Clear/Bounce
    /// </summary>
    public class ChequeTrackingViewModel : BaseViewModel
    {
        string _mode = "PDC"; bool _isEmpty; string _status = ""; string _summary = "";
        public ObservableCollection<ChequeRecord> Cheques { get; } = new();

        public string Mode { get => _mode; set { Set(ref _mode, value); OnPropertyChanged(nameof(IsPdcMode)); OnPropertyChanged(nameof(IsReconMode)); OnPropertyChanged(nameof(Heading)); Load(); } }
        public bool IsPdcMode => _mode == "PDC";
        public bool IsReconMode => _mode == "Reconciliation";
        public bool IsEmpty { get => _isEmpty; set => Set(ref _isEmpty, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public string SummaryText { get => _summary; set => Set(ref _summary, value); }
        public string Heading => IsPdcMode
            ? "Post-Dated Cheques — ordered by due date"
            : "Outstanding cheques — awaiting clearance";

        public ICommand ShowPdcCommand { get; }
        public ICommand ShowReconCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand PresentCommand { get; }
        public ICommand ClearedCommand { get; }
        public ICommand BounceCommand { get; }

        public ChequeTrackingViewModel()
        {
            ShowPdcCommand   = new RelayCommand(() => Mode = "PDC");
            ShowReconCommand = new RelayCommand(() => Mode = "Reconciliation");
            RefreshCommand   = new RelayCommand(Load);
            ExportCommand    = new RelayCommand(ExportCsv, () => Cheques.Count > 0);
            PresentCommand   = new RelayCommand<ChequeRecord>(DoPresent, c => c != null && AuthService.CanEdit);
            ClearedCommand   = new RelayCommand<ChequeRecord>(DoCleared, c => c != null && AuthService.CanEdit);
            BounceCommand    = new RelayCommand<ChequeRecord>(DoBounce,  c => c != null && AuthService.CanEdit);
        }

        public void Load()
        {
            Cheques.Clear();
            var list = IsPdcMode ? ChequeService.GetPdcCheques() : ChequeService.GetOutstandingCheques();
            foreach (var c in list) Cheques.Add(c);
            IsEmpty = Cheques.Count == 0;
            UpdateSummary();
        }

        void UpdateSummary()
        {
            if (Cheques.Count == 0) { SummaryText = ""; return; }
            var cur = Cheques[0].Currency ?? "OMR";
            decimal total = Cheques.Sum(c => c.Amount);
            if (IsPdcMode)
            {
                int overdue = Cheques.Count(c => c.DaysUntilDue < 0);
                decimal overdueAmt = Cheques.Where(c => c.DaysUntilDue < 0).Sum(c => c.Amount);
                SummaryText = overdue > 0
                    ? $"{Cheques.Count} PDC • Total {cur} {total:N3}   |   {overdue} overdue • {cur} {overdueAmt:N3}"
                    : $"{Cheques.Count} PDC • Total {cur} {total:N3}";
            }
            else
            {
                decimal presented = Cheques.Where(c => c.Status == "Presented").Sum(c => c.Amount);
                SummaryText = $"{Cheques.Count} outstanding • Total {cur} {total:N3}   |   Presented: {cur} {presented:N3}";
            }
        }

        void ExportCsv()
        {
            if (Cheques.Count == 0) { StatusMessage = "Nothing to export."; return; }
            var d = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV file (*.csv)|*.csv",
                FileName = $"{(IsPdcMode ? "PDC" : "Reconciliation")}_{DateTime.Now:yyyyMMdd}.csv"
            };
            if (d.ShowDialog() != true) return;
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Cheque #,Cheque Date,Due,Payee,Bank,Amount,Currency,Status,Presented,Cleared");
                foreach (var c in Cheques)
                    sb.AppendLine(string.Join(",",
                        Csv(c.ChequeNumber), c.ChequeDate.ToString("yyyy-MM-dd"), Csv(c.DueLabel),
                        Csv(c.PayeeName), Csv(c.BankName), c.Amount.ToString("0.000"), Csv(c.Currency),
                        Csv(c.Status), c.PresentedDate?.ToString("yyyy-MM-dd") ?? "", c.ClearedDate?.ToString("yyyy-MM-dd") ?? ""));
                System.IO.File.WriteAllText(d.FileName, sb.ToString(), new UTF8Encoding(true));
                StatusMessage = $"Exported {Cheques.Count} cheque(s) to {System.IO.Path.GetFileName(d.FileName)}.";
            }
            catch (Exception ex) { StatusMessage = $"Export failed: {ex.Message}"; }
        }

        static string Csv(string? s)
        {
            s ??= "";
            return s.Contains(',') || s.Contains('"') || s.Contains('\n')
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
        }

        void Guard(Action a) { if (!AuthService.CanEdit) { StatusMessage = "Your role is read-only."; return; } a(); Load(); }

        void DoPresent(ChequeRecord? c) { if (c == null) return; Guard(() => { ChequeService.MarkPresented(c.Id, DateTime.Today); StatusMessage = $"Cheque #{c.ChequeNumber} marked presented."; }); }
        void DoCleared(ChequeRecord? c) { if (c == null) return; Guard(() => { ChequeService.MarkCleared(c.Id, DateTime.Today); StatusMessage = $"Cheque #{c.ChequeNumber} marked cleared."; }); }
        void DoBounce(ChequeRecord? c)
        {
            if (c == null) return;
            var reason = InputBox.Show($"Reason cheque #{c.ChequeNumber} bounced / was returned:", "Bounced Cheque");
            if (reason == null) return;
            Guard(() => { ChequeService.MarkBounced(c.Id, reason); StatusMessage = $"Cheque #{c.ChequeNumber} marked bounced."; });
        }
    }
}
