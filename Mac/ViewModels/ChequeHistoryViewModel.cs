using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class ChequeHistoryViewModel : ViewModelBase
    {
        string _search = "", _statusFilter = "";
        bool _isEmpty;

        public ObservableCollection<ChequeRecord> Cheques { get; } = new();
        public List<string> Statuses { get; } = new() { "", "Draft", "ReadyToPrint", "Printed", "Reprinted", "Cancelled", "Void" };
        public string Search { get => _search; set { Set(ref _search, value); Load(); } }
        public string StatusFilter { get => _statusFilter; set { Set(ref _statusFilter, value); Load(); } }
        public bool IsEmpty { get => _isEmpty; set => Set(ref _isEmpty, value); }

        public ICommand RefreshCommand { get; }

        public ChequeHistoryViewModel() { RefreshCommand = new RelayCommand(Load); Load(); }

        public void Load()
        {
            Cheques.Clear();
            var list = ChequeService.GetCheques(
                string.IsNullOrWhiteSpace(Search) ? null : Search,
                string.IsNullOrWhiteSpace(StatusFilter) ? null : StatusFilter);
            foreach (var c in list) Cheques.Add(c);
            IsEmpty = Cheques.Count == 0;
        }
    }
}
