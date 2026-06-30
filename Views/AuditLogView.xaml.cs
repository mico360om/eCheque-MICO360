using System.Windows.Controls;
using eCheque.MICO360.ViewModels;
namespace eCheque.MICO360.Views
{
    public partial class AuditLogView : UserControl
    {
        public AuditLogView() { InitializeComponent(); Loaded += (s, e) => { if (DataContext is AuditLogViewModel vm) vm.Load(); }; }
    }
}
