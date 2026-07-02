using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using eCheque.MICO360.Models;
using eCheque.MICO360.ViewModels;
namespace eCheque.MICO360.Views
{
    public partial class ChequeHistoryView : UserControl
    {
        public ChequeHistoryView()
        {
            InitializeComponent();
            Loaded += (s, e) => { if (DataContext is ChequeHistoryViewModel vm) vm.Load(); };
            // Enter on search box triggers instant filter (already via PropertyChanged binding)
            TxtSearch.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter && DataContext is ChequeHistoryViewModel vm) { vm.Load(); e.Handled = true; }
            };
        }

        private void BtnPrintSelected_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is ChequeHistoryViewModel vm)
                vm.RequestBulkPrint(Grid.SelectedItems.OfType<ChequeRecord>());
        }
    }
}
