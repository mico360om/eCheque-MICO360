using Avalonia.Controls;
using Avalonia.Interactivity;

namespace eCheque.MICO360.Mac.Views
{
    public partial class MessageDialog : Window
    {
        public MessageDialog() => InitializeComponent();

        void OnPrimary(object? sender, RoutedEventArgs e) => Close(true);
        void OnSecondary(object? sender, RoutedEventArgs e) => Close(false);

        /// <summary>Shows a two-button dialog. Returns true if the primary button was clicked.</summary>
        public static async Task<bool> Show(Window owner, string heading, string body, string primary, string secondary)
        {
            var dlg = new MessageDialog { Title = heading };
            dlg.TxtHeading.Text = heading;
            dlg.TxtBody.Text = body;
            dlg.BtnPrimary.Content = primary;
            dlg.BtnSecondary.Content = secondary;
            return await dlg.ShowDialog<bool>(owner);
        }
    }
}
