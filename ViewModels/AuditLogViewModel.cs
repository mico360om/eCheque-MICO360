using System.Collections.ObjectModel;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class AuditLogViewModel : BaseViewModel
    {
        ObservableCollection<AuditLog> _logs=new();
        public ObservableCollection<AuditLog> Logs{get=>_logs;set=>Set(ref _logs,value);}
        public void Load(){Logs=new ObservableCollection<AuditLog>(ChequeService.GetAuditLogs());}
    }
}
