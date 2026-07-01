using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eCheque.MICO360.Mac.Views
{
    public partial class InputDialog : Window
    {
        public InputDialog() => InitializeComponent();

        void OnOk(object? sender, RoutedEventArgs e) => Close(TxtValue.Text?.Trim());
        void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

        /// <summary>Shows a modal prompt; returns the trimmed text or null if cancelled/empty.</summary>
        public static async Task<string?> Show(Window owner, string message, string title)
        {
            var dlg = new InputDialog { Title = title };
            dlg.TxtMessage.Text = message;
            var result = await dlg.ShowDialog<string?>(owner);
            return string.IsNullOrWhiteSpace(result) ? null : result;
        }
    }
}
