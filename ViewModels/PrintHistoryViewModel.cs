using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class PrintHistoryViewModel : BaseViewModel
    {
        ObservableCollection<PrintHistory> _history = new();
        ObservableCollection<string> _banks = new();
        string _search = "", _bankFilter = "", _typeFilter = ""; DateTime? _from, _to; bool _isEmpty;

        public ObservableCollection<PrintHistory> History{get=>_history;set=>Set(ref _history,value);}
        public ObservableCollection<string> Banks{get=>_banks;set=>Set(ref _banks,value);}
        public List<string> Types{get;}=new(){"","First Print","Reprint"};
        public string Search{get=>_search;set{Set(ref _search,value);Load();}}
        public string BankFilter{get=>_bankFilter;set{Set(ref _bankFilter,value);Load();}}
        public string TypeFilter{get=>_typeFilter;set{Set(ref _typeFilter,value);Load();}}
        public DateTime? From{get=>_from;set{Set(ref _from,value);Load();}}
        public DateTime? To{get=>_to;set{Set(ref _to,value);Load();}}
        public bool IsEmpty{get=>_isEmpty;set=>Set(ref _isEmpty,value);}

        public ICommand RefreshCommand{get;}
        public ICommand ExportCommand{get;}
        public ICommand ClearFiltersCommand{get;}

        public PrintHistoryViewModel()
        {
            RefreshCommand=new RelayCommand(Load);
            ExportCommand=new RelayCommand(DoExport);
            ClearFiltersCommand=new RelayCommand(ClearFilters);
        }

        public void Load()
        {
            var all = ChequeService.GetPrintHistory();
            Banks = new ObservableCollection<string>(
                new[]{""}.Concat(all.Select(h=>h.BankName).Where(b=>!string.IsNullOrWhiteSpace(b)).Distinct().OrderBy(b=>b)));

            var q = all.AsEnumerable();
            if(!string.IsNullOrWhiteSpace(Search))
                q=q.Where(h=>(h.ChequeNumber??"").Contains(Search,StringComparison.OrdinalIgnoreCase)
                          ||(h.PayeeName??"").Contains(Search,StringComparison.OrdinalIgnoreCase)
                          ||(h.PrintedBy??"").Contains(Search,StringComparison.OrdinalIgnoreCase)
                          ||(h.Reason??"").Contains(Search,StringComparison.OrdinalIgnoreCase));
            if(!string.IsNullOrWhiteSpace(BankFilter)) q=q.Where(h=>h.BankName==BankFilter);
            if(TypeFilter=="First Print") q=q.Where(h=>!h.IsReprint);
            else if(TypeFilter=="Reprint") q=q.Where(h=>h.IsReprint);
            if(From.HasValue) q=q.Where(h=>h.PrintedDate.Date>=From.Value.Date);
            if(To.HasValue) q=q.Where(h=>h.PrintedDate.Date<=To.Value.Date);

            History=new ObservableCollection<PrintHistory>(q);
            IsEmpty = History.Count==0;
        }

        void DoExport()
        {
            try
            {
                if(History.Count==0){System.Windows.MessageBox.Show("There are no print records to export.","Export");return;}
                var path=ExportService.ExportPrintHistory(History.ToList());
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});
            }
            catch(Exception ex){System.Windows.MessageBox.Show($"Export error: {ex.Message}");}
        }

        void ClearFilters()
        {
            _search="";OnPropertyChanged(nameof(Search));
            _bankFilter="";OnPropertyChanged(nameof(BankFilter));
            _typeFilter="";OnPropertyChanged(nameof(TypeFilter));
            _from=null;OnPropertyChanged(nameof(From));
            _to=null;OnPropertyChanged(nameof(To));
            Load();
        }
    }
}
