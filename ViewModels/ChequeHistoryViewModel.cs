using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class ChequeHistoryViewModel : BaseViewModel
    {
        ObservableCollection<ChequeRecord> _cheques=new(); ChequeRecord? _sel; string _search="",_statusF=""; DateTime? _from,_to;
        public ObservableCollection<ChequeRecord> Cheques{get=>_cheques;set=>Set(ref _cheques,value);}
        public ChequeRecord? Selected{get=>_sel;set=>Set(ref _sel,value);}
        public string Search{get=>_search;set{Set(ref _search,value);Refresh();}}
        public string StatusFilter{get=>_statusF;set{Set(ref _statusF,value);Refresh();}}
        public DateTime? From{get=>_from;set{Set(ref _from,value);Refresh();}}
        public DateTime? To{get=>_to;set{Set(ref _to,value);Refresh();}}
        public List<string> Statuses{get;}=new(){"","Draft","ReadyToPrint","Printed","Cancelled","Void","Reprinted"};
        public ICommand RefreshCommand{get;}
        public ICommand PrintCommand{get;}
        public ICommand ExportCommand{get;}
        public ICommand CancelCommand{get;}
        public ICommand VoidCommand{get;}
        public ICommand ClearFiltersCommand{get;}
        public ICommand EditCommand{get;}
        public event Action<ChequeRecord,ChequeProfile>? PrintRequested;
        public event Action<int>? EditRequested;
        public ChequeHistoryViewModel()
        {
            RefreshCommand=new RelayCommand(Refresh);
            PrintCommand=new RelayCommand<ChequeRecord>(DoPrint, r=>r!=null&&r.Status!="Cancelled"&&r.Status!="Void");
            ExportCommand=new RelayCommand(DoExport);
            CancelCommand=new RelayCommand<ChequeRecord>(DoCancel, r=>r!=null&&r.Status!="Cancelled"&&r.Status!="Void");
            VoidCommand=new RelayCommand<ChequeRecord>(DoVoid, r=>r!=null&&r.Status!="Cancelled"&&r.Status!="Void");
            ClearFiltersCommand=new RelayCommand(ClearFilters);
            EditCommand=new RelayCommand<ChequeRecord>(DoEdit, r=>r!=null);
        }
        public void Load()=>Refresh();
        void Refresh(){try{Cheques=new ObservableCollection<ChequeRecord>(ChequeService.GetCheques(string.IsNullOrWhiteSpace(Search)?null:Search,string.IsNullOrWhiteSpace(StatusFilter)?null:StatusFilter,From,To));}catch{}}
        void DoPrint(ChequeRecord? r){if(r==null)return;Selected=r;var p=ChequeService.GetProfile(r.ProfileId);if(p!=null)PrintRequested?.Invoke(r,p);}
        void DoExport(){try{var path=ExportService.ExportCheques(Cheques.ToList());System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){System.Windows.MessageBox.Show($"Export error: {ex.Message}");}}
        void DoCancel(ChequeRecord? r){if(r==null)return;var reason=Helpers.InputBox.Show($"Reason for cancelling cheque #{r.ChequeNumber}:","Cancel Cheque");if(reason==null)return;ChequeService.CancelCheque(r.Id,reason);Refresh();}
        void DoVoid(ChequeRecord? r){if(r==null)return;var reason=Helpers.InputBox.Show($"Reason for voiding cheque #{r.ChequeNumber}:","Void Cheque");if(reason==null)return;ChequeService.VoidCheque(r.Id,reason);Refresh();}
        void DoEdit(ChequeRecord? r){if(r!=null)EditRequested?.Invoke(r.Id);}
        void ClearFilters(){_search="";OnPropertyChanged(nameof(Search));_statusF="";OnPropertyChanged(nameof(StatusFilter));_from=null;OnPropertyChanged(nameof(From));_to=null;OnPropertyChanged(nameof(To));Refresh();}
    }
}
