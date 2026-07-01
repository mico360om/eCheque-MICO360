using System.Collections.ObjectModel;
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
        string _mode = "PDC"; bool _isEmpty; string _status = "";
        public ObservableCollection<ChequeRecord> Cheques { get; } = new();

        public string Mode { get => _mode; set { Set(ref _mode, value); OnPropertyChanged(nameof(IsPdcMode)); OnPropertyChanged(nameof(IsReconMode)); OnPropertyChanged(nameof(Heading)); Load(); } }
        public bool IsPdcMode => _mode == "PDC";
        public bool IsReconMode => _mode == "Reconciliation";
        public bool IsEmpty { get => _isEmpty; set => Set(ref _isEmpty, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public string Heading => IsPdcMode
            ? "Post-Dated Cheques — ordered by due date"
            : "Outstanding cheques — awaiting clearance";

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
