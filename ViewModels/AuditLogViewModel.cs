using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class AuditLogViewModel : BaseViewModel
    {
        ObservableCollection<AuditLog> _logs=new();
        List<AuditLog> _all=new();
        string _search="", _actionFilter=""; DateTime? _from,_to; bool _isEmpty;

        public ObservableCollection<AuditLog> Logs{get=>_logs;set=>Set(ref _logs,value);}
        public ObservableCollection<string> Actions{get;}=new();
        public string Search{get=>_search;set{Set(ref _search,value);Apply();}}
        public string ActionFilter{get=>_actionFilter;set{Set(ref _actionFilter,value);Apply();}}
        public DateTime? From{get=>_from;set{Set(ref _from,value);Apply();}}
        public DateTime? To{get=>_to;set{Set(ref _to,value);Apply();}}
        public bool IsEmpty{get=>_isEmpty;set=>Set(ref _isEmpty,value);}

        public ICommand RefreshCommand{get;}
        public ICommand ClearFiltersCommand{get;}

        public AuditLogViewModel()
        {
            RefreshCommand=new RelayCommand(Load);
            ClearFiltersCommand=new RelayCommand(ClearFilters);
        }

        public void Load()
        {
            _all=ChequeService.GetAuditLogs(1000);
            Actions.Clear();
            Actions.Add("");
            foreach(var a in _all.Select(l=>l.Action).Where(a=>!string.IsNullOrWhiteSpace(a)).Distinct().OrderBy(a=>a))
                Actions.Add(a);
            Apply();
        }

        void Apply()
        {
            var q=_all.AsEnumerable();
            if(!string.IsNullOrWhiteSpace(Search))
                q=q.Where(l=>(l.UserName??"").Contains(Search,StringComparison.OrdinalIgnoreCase)
                          ||(l.Action??"").Contains(Search,StringComparison.OrdinalIgnoreCase)
                          ||(l.RecordReference??"").Contains(Search,StringComparison.OrdinalIgnoreCase)
                          ||(l.Remarks??"").Contains(Search,StringComparison.OrdinalIgnoreCase));
            if(!string.IsNullOrWhiteSpace(ActionFilter)) q=q.Where(l=>l.Action==ActionFilter);
            if(From.HasValue) q=q.Where(l=>l.ActionDate.Date>=From.Value.Date);
            if(To.HasValue) q=q.Where(l=>l.ActionDate.Date<=To.Value.Date);
            Logs=new ObservableCollection<AuditLog>(q);
            IsEmpty=Logs.Count==0;
        }

        void ClearFilters()
        {
            _search="";OnPropertyChanged(nameof(Search));
            _actionFilter="";OnPropertyChanged(nameof(ActionFilter));
            _from=null;OnPropertyChanged(nameof(From));
            _to=null;OnPropertyChanged(nameof(To));
            Apply();
        }
    }
}
