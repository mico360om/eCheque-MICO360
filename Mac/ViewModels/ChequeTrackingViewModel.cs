using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Controls.ApplicationLifetimes;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;
using eCheque.MICO360.Mac.Views;

namespace eCheque.MICO360.Mac.ViewModels
{
    /// <summary>PDC Register + Reconciliation for macOS (mirrors the Windows Cheque Tracking screen).</summary>
    public class ChequeTrackingViewModel : ViewModelBase
    {
        string _mode = "PDC", _status = "", _summary = ""; bool _isEmpty;
        public ObservableCollection<ChequeRecord> Cheques { get; } = new();

        public string Mode { get => _mode; set { Set(ref _mode, value); OnPropertyChanged(nameof(IsPdcMode)); OnPropertyChanged(nameof(Heading)); Load(); } }
        public bool IsPdcMode => _mode == "PDC";
        public bool IsEmpty { get => _isEmpty; set => Set(ref _isEmpty, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public string SummaryText { get => _summary; set => Set(ref _summary, value); }
        public string Heading => IsPdcMode ? "Post-Dated Cheques — ordered by due date" : "Outstanding cheques — awaiting clearance";

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
            ExportCommand    = new RelayCommand(async () => await ExportCsv());
            PresentCommand   = new RelayCommand<ChequeRecord>(c => Guard(c, () => ChequeService.MarkPresented(c!.Id, DateTime.Today), "presented"));
            ClearedCommand   = new RelayCommand<ChequeRecord>(c => Guard(c, () => ChequeService.MarkCleared(c!.Id, DateTime.Today), "cleared"));
            BounceCommand    = new RelayCommand<ChequeRecord>(async c => await DoBounce(c));
            Load();
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

        async Task ExportCsv()
        {
            if (Cheques.Count == 0) { StatusMessage = "Nothing to export."; return; }
            var owner = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner?.StorageProvider == null) return;
            var file = await owner.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
            {
                SuggestedFileName = $"{(IsPdcMode ? "PDC" : "Reconciliation")}_{DateTime.Now:yyyyMMdd}.csv",
                DefaultExtension = "csv",
                FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("CSV file") { Patterns = new[] { "*.csv" } } }
            });
            if (file == null) return;
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("Cheque #,Cheque Date,Due,Payee,Bank,Amount,Currency,Status,Presented,Cleared");
                foreach (var c in Cheques)
                    sb.AppendLine(string.Join(",",
                        Csv(c.ChequeNumber), c.ChequeDate.ToString("yyyy-MM-dd"), Csv(c.DueLabel),
                        Csv(c.PayeeName), Csv(c.BankName), c.Amount.ToString("0.000"), Csv(c.Currency),
                        Csv(c.Status), c.PresentedDate?.ToString("yyyy-MM-dd") ?? "", c.ClearedDate?.ToString("yyyy-MM-dd") ?? ""));
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new System.IO.StreamWriter(stream, new System.Text.UTF8Encoding(true));
                await writer.WriteAsync(sb.ToString());
                StatusMessage = $"Exported {Cheques.Count} cheque(s) to {file.Name}.";
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

        void Guard(ChequeRecord? c, Action act, string verb)
        {
            if (c == null) return;
            if (!AuthService.CanEdit) { StatusMessage = "Your role is read-only."; return; }
            act(); StatusMessage = $"Cheque #{c.ChequeNumber} marked {verb}."; Load();
        }

        async Task DoBounce(ChequeRecord? c)
        {
            if (c == null || !AuthService.CanEdit) { if (!AuthService.CanEdit) StatusMessage = "Your role is read-only."; return; }
            var owner = (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner == null) return;
            var reason = await InputDialog.Show(owner, $"Reason cheque #{c.ChequeNumber} bounced / was returned:", "Bounced Cheque");
            if (reason == null) return;
            ChequeService.MarkBounced(c.Id, reason);
            StatusMessage = $"Cheque #{c.ChequeNumber} marked bounced.";
            Load();
        }
    }
}
