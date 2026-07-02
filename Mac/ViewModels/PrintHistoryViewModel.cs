using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class PrintHistoryViewModel : ViewModelBase
    {
        string _search = "";
        bool _isEmpty;

        public ObservableCollection<PrintHistory> History { get; } = new();
        public string Search { get => _search; set { Set(ref _search, value); Load(); } }
        public bool IsEmpty { get => _isEmpty; set => Set(ref _isEmpty, value); }

        public ICommand RefreshCommand { get; }

        public PrintHistoryViewModel() { RefreshCommand = new RelayCommand(Load); Load(); }

        public void Load()
        {
            History.Clear();
            var all = ChequeService.GetPrintHistory();

            var q = all.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(Search))
                q = q.Where(h => (h.ChequeNumber ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase)
                              || (h.PayeeName ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase)
                              || (h.PrintedBy ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase)
                              || (h.Reason ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase));

            foreach (var h in q) History.Add(h);
            IsEmpty = History.Count == 0;
        }
    }
}
