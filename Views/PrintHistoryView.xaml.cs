using System.Windows.Controls;
using eCheque.MICO360.ViewModels;
namespace eCheque.MICO360.Views
{
    public partial class PrintHistoryView : UserControl
    {
        public PrintHistoryView() { InitializeComponent(); Loaded += (s, e) => { if (DataContext is PrintHistoryViewModel vm) vm.Load(); }; }
    }
}
