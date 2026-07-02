using System;
using Panel = System.Windows.Controls.Panel;
using Border = System.Windows.Controls.Border;
using DockPanel = System.Windows.Controls.DockPanel;
using Dock = System.Windows.Controls.Dock;
using TextBlock = System.Windows.Controls.TextBlock;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Thickness = System.Windows.Thickness;
using CornerRadius = System.Windows.CornerRadius;
using VerticalAlignment = System.Windows.VerticalAlignment;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using FontWeights = System.Windows.FontWeights;
using TextWrapping = System.Windows.TextWrapping;
using TextTrimming = System.Windows.TextTrimming;
using UIElement = System.Windows.UIElement;
using Cursors = System.Windows.Input.Cursors;
using DoubleAnimation = System.Windows.Media.Animation.DoubleAnimation;
using DispatcherTimer = System.Windows.Threading.DispatcherTimer;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Lightweight non-blocking toast/snackbar notifications. A host panel (top-right of the main
    /// window) is registered once; anything can then call Success/Error/Info/Warn from anywhere.
    /// Uses only opacity animation + borders (no shadow effects) so it renders on RDP/VM/GPU-less PCs.
    /// </summary>
    public static class ToastService
    {
        static Panel? _host;
        public static void Register(Panel host) => _host = host;

        public static void Success(string message) => Show(message, "#2E7D32", "✔");
        public static void Error(string message)   => Show(message, "#C62828", "✕");
        public static void Info(string message)    => Show(message, "#1565C0", "ℹ");
        public static void Warn(string message)    => Show(message, "#E65100", "⚠");

        public static void Show(string message, string colorHex, string glyph)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;
            app.Dispatcher.Invoke(() =>
            {
                if (_host == null) return;
                var color = (Color)ColorConverter.ConvertFromString(colorHex);
                var toast = BuildToast(message, color, glyph, out var closeBtn);
                _host.Children.Insert(0, toast);
                toast.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)));

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                void Dismiss()
                {
                    timer.Stop();
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                    fade.Completed += (s, e) => { if (_host != null && _host.Children.Contains(toast)) _host.Children.Remove(toast); };
                    toast.BeginAnimation(UIElement.OpacityProperty, fade);
                }
                timer.Tick += (s, e) => Dismiss();
                closeBtn.Click += (s, e) => Dismiss();
                timer.Start();
            });
        }

        static Border BuildToast(string message, Color color, string glyph, out System.Windows.Controls.Button closeBtn)
        {
            var accent = new SolidColorBrush(color);
            var panel = new DockPanel { LastChildFill = true };

            var icon = new Border
            {
                Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = accent,
                Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = glyph, Foreground = Brushes.White, FontSize = 13, FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
                }
            };
            DockPanel.SetDock(icon, Dock.Left);

            closeBtn = new System.Windows.Controls.Button
            {
                Content = "✕", Width = 22, Height = 22, Cursor = Cursors.Hand,
                Background = Brushes.Transparent, BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)), FontSize = 11,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, -2, 0, 0)
            };
            DockPanel.SetDock(closeBtn, Dock.Right);

            var text = new TextBlock
            {
                Text = message, TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.CharacterEllipsis,
                FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                VerticalAlignment = VerticalAlignment.Center, MaxWidth = 300
            };

            panel.Children.Add(icon);
            panel.Children.Add(closeBtn);
            panel.Children.Add(text);

            return new Border
            {
                Background = Brushes.White,
                BorderBrush = accent, BorderThickness = new Thickness(0, 0, 0, 3),
                CornerRadius = new CornerRadius(8), Padding = new Thickness(14, 12, 12, 12),
                Margin = new Thickness(0, 0, 0, 10), MinWidth = 260, MaxWidth = 380,
                Opacity = 0, Child = panel
            };
        }
    }
}
