using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using eCheque.MICO360.ViewModels;

namespace eCheque.MICO360.Views
{
    public partial class ChequeEntryView : UserControl
    {
        private readonly DispatcherTimer _convertTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };

        public ChequeEntryView()
        {
            InitializeComponent();

            // Debounced auto-convert as user types amount
            _convertTimer.Tick += (s, e) =>
            {
                _convertTimer.Stop();
                ApplyAmount(commit: false);
            };

            // When DataContext or Cheque changes, sync the amount display
            DataContextChanged += (s, e) =>
            {
                if (e.NewValue is ChequeEntryViewModel vm)
                    vm.PropertyChanged += (_, pe) =>
                    {
                        if (pe.PropertyName == nameof(ChequeEntryViewModel.Cheque))
                            TxtAmount.Text = vm.Cheque.Amount > 0 ? vm.Cheque.Amount.ToString("N3") : "";
                    };
            };

            // Keyboard shortcuts
            PreviewKeyDown += (s, e) =>
            {
                if (DataContext is not ChequeEntryViewModel vm) return;
                if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { vm.SaveDraftCommand.Execute(null); e.Handled = true; }
                else if (e.Key == Key.P && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
                { vm.SavePrintCommand.Execute(null); e.Handled = true; }
            };
        }

        // Fires on every keystroke in amount field — starts debounce timer
        private void AmountBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _convertTimer.Stop();
            _convertTimer.Start();
        }

        // Fires on focus-leave — commit the value and format it
        private void AmountBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _convertTimer.Stop();
            ApplyAmount(commit: true);
        }

        private void ApplyAmount(bool commit)
        {
            if (DataContext is not ChequeEntryViewModel vm) return;
            var text = TxtAmount.Text.Replace(",", "").Trim();
            if (decimal.TryParse(text, out var amount))
            {
                vm.Cheque.Amount = amount;
                if (commit) TxtAmount.Text = amount.ToString("N3");
                vm.ConvertWordsCommand.Execute(null);
            }
        }

        // Memo character counter
        private void MemoBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtMemoCount.Text = $"{TxtMemo.Text.Length} / 250";
        }
    }
}
