using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace eCheque.MICO360.Helpers
{
    /// <summary>Minimal modal single-line text prompt (WPF has no built-in InputBox).</summary>
    public static class InputBox
    {
        /// <summary>
        /// Shows a prompt. Returns the trimmed text, or null if cancelled.
        /// When <paramref name="required"/> is true, re-prompts until a non-empty value is entered.
        /// </summary>
        public static string? Show(string message, string title, bool required = true)
        {
            while (true)
            {
                var win = new Window
                {
                    Title = title,
                    Width = 440,
                    Height = 210,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow
                };
                if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsLoaded)
                    win.Owner = Application.Current.MainWindow;

                var panel = new StackPanel { Margin = new Thickness(18) };
                panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
                var box = new TextBox { MinHeight = 54, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Padding = new Thickness(6) };
                panel.Children.Add(box);

                var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
                var ok = new Button { Content = "OK", Width = 80, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
                var cancel = new Button { Content = "Cancel", Width = 80, IsCancel = true };
                string? result = null;
                ok.Click += (s, e) => { result = box.Text?.Trim(); win.DialogResult = true; };
                btns.Children.Add(ok);
                btns.Children.Add(cancel);
                panel.Children.Add(btns);

                win.Content = panel;
                box.Loaded += (s, e) => box.Focus();

                if (win.ShowDialog() != true) return null;
                if (!required || !string.IsNullOrWhiteSpace(result)) return result ?? "";
                MessageBox.Show("A value is required to continue.", title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
