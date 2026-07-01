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
        string _mode = "PDC", _status = ""; bool _isEmpty;
        public ObservableCollection<ChequeRecord> Cheques { get; } = new();

        public string Mode { get => _mode; set { Set(ref _mode, value); OnPropertyChanged(nameof(IsPdcMode)); OnPropertyChanged(nameof(Heading)); Load(); } }
        public bool IsPdcMode => _mode == "PDC";
        public bool IsEmpty { get => _isEmpty; set => Set(ref _isEmpty, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public string Heading => IsPdcMode ? "Post-Dated Cheques — ordered by due date" : "Outstanding cheques — awaiting clearance";

        public ICommand ShowPdcCommand { get; }
        public ICommand ShowReconCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand PresentCommand { get; }
        public ICommand ClearedCommand { get; }
        public ICommand BounceCommand { get; }

        public ChequeTrackingViewModel()
        {
            ShowPdcCommand   = new RelayCommand(() => Mode = "PDC");
            ShowReconCommand = new RelayCommand(() => Mode = "Reconciliation");
            RefreshCommand   = new RelayCommand(Load);
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
