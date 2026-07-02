using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class AuditLogViewModel : ViewModelBase
    {
        List<AuditLog> _all = new();
        string _search = "", _actionFilter = "";
        DateTimeOffset? _from, _to;
        bool _isEmpty;

        public ObservableCollection<AuditLog> Logs { get; } = new();
        public ObservableCollection<string> Actions { get; } = new();

        public string Search { get => _search; set { Set(ref _search, value); Apply(); } }
        public string ActionFilter { get => _actionFilter; set { Set(ref _actionFilter, value); Apply(); } }
        public DateTimeOffset? From { get => _from; set { Set(ref _from, value); Apply(); } }
        public DateTimeOffset? To { get => _to; set { Set(ref _to, value); Apply(); } }
        public bool IsEmpty { get => _isEmpty; set => Set(ref _isEmpty, value); }

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        public AuditLogViewModel()
        {
            RefreshCommand = new RelayCommand(Load);
            ClearFiltersCommand = new RelayCommand(ClearFilters);
        }

        public void Load()
        {
            _all = ChequeService.GetAuditLogs(1000);
            Actions.Clear();
            Actions.Add("");
            foreach (var a in _all.Select(l => l.Action)
                                  .Where(a => !string.IsNullOrWhiteSpace(a))
                                  .Distinct().OrderBy(a => a))
                Actions.Add(a);
            Apply();
        }

        void Apply()
        {
            var q = _all.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(Search))
                q = q.Where(l => (l.UserName ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase)
                              || (l.Action ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase)
                              || (l.RecordReference ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase)
                              || (l.Remarks ?? "").Contains(Search, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(ActionFilter)) q = q.Where(l => l.Action == ActionFilter);
            if (From.HasValue) q = q.Where(l => l.ActionDate.Date >= From.Value.Date);
            if (To.HasValue) q = q.Where(l => l.ActionDate.Date <= To.Value.Date);

            Logs.Clear();
            foreach (var l in q) Logs.Add(l);
            IsEmpty = Logs.Count == 0;
        }

        void ClearFilters()
        {
            _search = ""; OnPropertyChanged(nameof(Search));
            _actionFilter = ""; OnPropertyChanged(nameof(ActionFilter));
            _from = null; OnPropertyChanged(nameof(From));
            _to = null; OnPropertyChanged(nameof(To));
            Apply();
        }
    }
}
