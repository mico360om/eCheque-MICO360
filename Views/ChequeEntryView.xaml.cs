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

            // Block pasting anything that isn't a valid numeric amount into the amount box.
            DataObject.AddPastingHandler(TxtAmount, AmountBox_Pasting);

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
                {
                    // Initial sync: Load(id) usually runs BEFORE this view exists, so its Cheque-changed
                    // event was already raised. Push the current amount now so editing shows it.
                    SyncAmount(vm);
                    vm.PropertyChanged += (_, pe) =>
                    {
                        if (pe.PropertyName == nameof(ChequeEntryViewModel.Cheque))
                            SyncAmount(vm);
                    };
                }
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

        // Show the model's amount in the (unbound) amount box.
        private void SyncAmount(ChequeEntryViewModel vm)
        {
            var text = vm.Cheque.Amount > 0 ? vm.Cheque.Amount.ToString("N3") : "";
            if (TxtAmount.Text != text) TxtAmount.Text = text;
        }

        // True if applying `insert` at the current caret/selection would leave a still-valid amount
        // (digits, optional decimal point, up to 3 decimals — see Helpers.AmountInput).
        private bool WouldStayValidAmount(string insert)
        {
            int start = TxtAmount.SelectionStart;
            int len = TxtAmount.SelectionLength;
            string text = TxtAmount.Text;
            string candidate = text.Substring(0, start) + insert + text.Substring(start + len);
            return Helpers.AmountInput.IsAcceptable(candidate);
        }

        // Keystroke filter: only digits and a single decimal point may be typed. Letters, symbols,
        // spaces, extra decimal points, and >3 decimal places are rejected before they reach the box.
        private void AmountBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            foreach (char ch in e.Text)
                if (!char.IsDigit(ch) && ch != '.') { e.Handled = true; return; }
            e.Handled = !WouldStayValidAmount(e.Text);
        }

        // Paste filter: allow a paste only if the result is a valid amount (grouping commas tolerated,
        // since ApplyAmount strips them on commit). Anything else is blocked.
        private void AmountBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.UnicodeText)) { e.CancelCommand(); return; }
            var pasted = (e.DataObject.GetData(DataFormats.UnicodeText) as string ?? "").Trim();
            if (pasted.Length == 0 || !WouldStayValidAmount(pasted)) e.CancelCommand();
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
                vm.RefreshPayeeInsight(); // amount changed — re-check the duplicate-payment advisory
            }
        }

        // Payee changed (typed or picked) — refresh the inline history + duplicate advisory.
        private void Payee_LostFocus(object sender, RoutedEventArgs e)
            => (DataContext as ChequeEntryViewModel)?.RefreshPayeeInsight();

        private void Payee_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => Dispatcher.BeginInvoke(new Action(() => (DataContext as ChequeEntryViewModel)?.RefreshPayeeInsight()));

        // Memo character counter
        private void MemoBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            TxtMemoCount.Text = $"{TxtMemo.Text.Length} / 250";
        }
    }
}
