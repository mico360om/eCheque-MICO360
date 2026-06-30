using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class PrintHistoryViewModel : BaseViewModel
    {
        ObservableCollection<PrintHistory> _history = new(); string _search = ""; DateTime? _from, _to;
        public ObservableCollection<PrintHistory> History{get=>_history;set=>Set(ref _history,value);}
        public string Search{get=>_search;set{Set(ref _search,value);Load();}}
        public DateTime? From{get=>_from;set{Set(ref _from,value);Load();}}
        public DateTime? To{get=>_to;set{Set(ref _to,value);Load();}}
        public ICommand RefreshCommand{get;}
        public PrintHistoryViewModel(){RefreshCommand=new RelayCommand(Load);}
        public void Load()
        {
            var all = ChequeService.GetPrintHistory();
            var q = all.AsEnumerable();
            if(!string.IsNullOrWhiteSpace(Search))
                q=q.Where(h=>(h.ChequeNumber??string.Empty).Contains(Search,StringComparison.OrdinalIgnoreCase)||(h.PayeeName??string.Empty).Contains(Search,StringComparison.OrdinalIgnoreCase)||(h.PrintedBy??string.Empty).Contains(Search,StringComparison.OrdinalIgnoreCase));
            if(From.HasValue) q=q.Where(h=>h.PrintedDate.Date>=From.Value.Date);
            if(To.HasValue) q=q.Where(h=>h.PrintedDate.Date<=To.Value.Date);
            History=new ObservableCollection<PrintHistory>(q);
        }
    }
}
