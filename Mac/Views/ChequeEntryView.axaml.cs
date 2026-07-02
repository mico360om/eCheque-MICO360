using Avalonia.Controls;
using Avalonia.Interactivity;
using eCheque.MICO360.Mac.Services;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class ChequeEntryView : UserControl
    {
        public ChequeEntryView() => InitializeComponent();

        void OnPrint(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ChequeEntryViewModel vm && vm.SelectedProfile != null && vm.PreparePrint())
                ChequePrintService.PreviewOrPrint(vm.Cheque, vm.SelectedProfile);
        }
    }
}
