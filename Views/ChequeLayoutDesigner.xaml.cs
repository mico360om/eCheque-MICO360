using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Cursors = System.Windows.Input.Cursors;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;
using DoubleCollection = System.Windows.Media.DoubleCollection;
using Rectangle = System.Windows.Shapes.Rectangle;
using Panel = System.Windows.Controls.Panel;
using Thickness = System.Windows.Thickness;

namespace eCheque.MICO360.Views
{
    public partial class ChequeLayoutDesigner : Window
    {
        private readonly ChequeProfile _profile;
        private readonly ChequeProfile _original;
        private double _scale = 3.0;

        private bool _dragging;
        private Border? _dragEl;
        private Point _dragOffset;

        private static readonly (string Tag, string Label, string Color)[] Fields =
        {
            ("Date",      "📅 Date",         "#1565C0"),
            ("Payee",     "👤 Pay to:",       "#2E7D32"),
            ("Amount",    "💰 Amount (OMR)",   "#E65100"),
            ("Words",     "📝 Amount in Words","#6A1B9A"),
            ("ChequeNum", "# Cheque No.",     "#8B1818")
        };

        public bool Saved { get; private set; }

        public ChequeLayoutDesigner(ChequeProfile profile)
        {
            InitializeComponent();
            _profile = profile;
            _original = Clone(profile);
            TxtProfileName.Text = $"{profile.Name} — {profile.BankName}";
            RunScale.Text = _scale.ToString("F1");
            Loaded += (s, e) => BuildCanvas();
        }

        void BuildCanvas()
        {
            ChequeCanvas.Children.Clear();
            ChequeCanvas.Width  = _profile.ChequeWidth  * _scale;
            ChequeCanvas.Height = _profile.ChequeHeight * _scale;

            // Cheque background lines (MICR zone)
            var micrRect = new Rectangle
            {
                Width = ChequeCanvas.Width, Height = 12 * _scale,
                Fill = new SolidColorBrush(Color.FromRgb(0xF9, 0xF0, 0xF0)),
                Stroke = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                StrokeThickness = 0.5
            };
            Canvas.SetLeft(micrRect, 0);
            Canvas.SetTop(micrRect, (_profile.ChequeHeight - 12) * _scale);
            ChequeCanvas.Children.Add(micrRect);

            // Signature line
            var sigLine = new Line
            {
                X1 = (_profile.ChequeWidth - 52) * _scale, Y1 = (_profile.ChequeHeight - 12) * _scale,
                X2 = (_profile.ChequeWidth - 4)  * _scale, Y2 = (_profile.ChequeHeight - 12) * _scale,
                Stroke = Brushes.Gray, StrokeThickness = 0.8
            };
            ChequeCanvas.Children.Add(sigLine);

            // Add grid lines every 10mm
            for (int x = 0; x <= (int)_profile.ChequeWidth; x += 10)
            {
                var gl = new Line { X1 = x * _scale, Y1 = 0, X2 = x * _scale, Y2 = ChequeCanvas.Height,
                    Stroke = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0)), StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 2, 4 } };
                ChequeCanvas.Children.Add(gl);
                if (x > 0)
                {
                    var lbl = new TextBlock { Text = $"{x}", FontSize = 7, Foreground = Brushes.LightGray };
                    Canvas.SetLeft(lbl, x * _scale + 1); Canvas.SetTop(lbl, 1);
                    ChequeCanvas.Children.Add(lbl);
                }
            }
            for (int y = 0; y <= (int)_profile.ChequeHeight; y += 10)
            {
                var gl = new Line { X1 = 0, Y1 = y * _scale, X2 = ChequeCanvas.Width, Y2 = y * _scale,
                    Stroke = new SolidColorBrush(Color.FromArgb(0x18, 0, 0, 0)), StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 2, 4 } };
                ChequeCanvas.Children.Add(gl);
            }

            // Place draggable field labels
            foreach (var f in Fields)
            {
                var (x, y) = GetFieldPosition(f.Tag);
                AddFieldLabel(f.Tag, f.Label, f.Color, x, y);
            }
        }

        Border AddFieldLabel(string tag, string label, string colorHex, double xMm, double yMm)
        {
            var col = (Color)ColorConverter.ConvertFromString(colorHex);
            var border = new Border
            {
                Tag = tag,
                Background = new SolidColorBrush(Color.FromArgb(0x28, col.R, col.G, col.B)),
                BorderBrush = new SolidColorBrush(col),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 4, 8, 4),
                Cursor = Cursors.SizeAll
            };
            border.Child = new TextBlock
            {
                Text = label,
                Foreground = new SolidColorBrush(col),
                FontWeight = FontWeights.SemiBold,
                FontSize = 11
            };
            Canvas.SetLeft(border, xMm * _scale);
            Canvas.SetTop(border,  yMm * _scale);
            Panel.SetZIndex(border, 10);
            border.MouseLeftButtonDown += Field_MouseDown;
            ChequeCanvas.Children.Add(border);
            return border;
        }

        (double x, double y) GetFieldPosition(string tag) => tag switch
        {
            "Date"      => (_profile.DateX,       _profile.DateY),
            "Payee"     => (_profile.PayeeX,      _profile.PayeeY),
            "Amount"    => (_profile.AmountNumX,  _profile.AmountNumY),
            "Words"     => (_profile.AmountWordsX,_profile.AmountWordsY),
            "ChequeNum" => (_profile.ChequeNumX,  _profile.ChequeNumY),
            _           => (0, 0)
        };

        void SetFieldPosition(string tag, double xMm, double yMm)
        {
            switch (tag)
            {
                case "Date":      _profile.DateX=xMm;      _profile.DateY=yMm;      break;
                case "Payee":     _profile.PayeeX=xMm;     _profile.PayeeY=yMm;     break;
                case "Amount":    _profile.AmountNumX=xMm; _profile.AmountNumY=yMm; break;
                case "Words":     _profile.AmountWordsX=xMm;_profile.AmountWordsY=yMm; break;
                case "ChequeNum": _profile.ChequeNumX=xMm; _profile.ChequeNumY=yMm; break;
            }
        }

        void Field_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragEl = (Border)sender;
            _dragging = true;
            _dragOffset = e.GetPosition(_dragEl);
            _dragEl.CaptureMouse();
            Panel.SetZIndex(_dragEl, 100);
            e.Handled = true;
        }

        void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _dragEl == null) return;
            var pos = e.GetPosition(ChequeCanvas);
            _dragEl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var newX = Math.Max(0, Math.Min(pos.X - _dragOffset.X, ChequeCanvas.Width  - _dragEl.DesiredSize.Width));
            var newY = Math.Max(0, Math.Min(pos.Y - _dragOffset.Y, ChequeCanvas.Height - _dragEl.DesiredSize.Height));
            Canvas.SetLeft(_dragEl, newX);
            Canvas.SetTop(_dragEl,  newY);
            TxtPosition.Text = $"{_dragEl.Tag}: X = {newX / _scale:F1} mm,  Y = {newY / _scale:F1} mm";
        }

        void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging || _dragEl == null) return;
            _dragging = false;
            _dragEl.ReleaseMouseCapture();
            Panel.SetZIndex(_dragEl, 10);
            var xMm = Math.Round(Canvas.GetLeft(_dragEl) / _scale, 1);
            var yMm = Math.Round(Canvas.GetTop(_dragEl)  / _scale, 1);
            SetFieldPosition(_dragEl.Tag?.ToString() ?? "", xMm, yMm);
            TxtPosition.Text = $"{_dragEl.Tag} placed at  X = {xMm:F1} mm,  Y = {yMm:F1} mm  ✓";
            _dragEl = null;
        }

        void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            ChequeService.SaveProfilePositions(_profile);
            Saved = true;
            Close();
        }

        void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Restore original positions
            _profile.DateX=_original.DateX; _profile.DateY=_original.DateY;
            _profile.PayeeX=_original.PayeeX; _profile.PayeeY=_original.PayeeY;
            _profile.AmountNumX=_original.AmountNumX; _profile.AmountNumY=_original.AmountNumY;
            _profile.AmountWordsX=_original.AmountWordsX; _profile.AmountWordsY=_original.AmountWordsY;
            _profile.ChequeNumX=_original.ChequeNumX; _profile.ChequeNumY=_original.ChequeNumY;
            Close();
        }

        void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _profile.DateX=140; _profile.DateY=18;
            _profile.PayeeX=25; _profile.PayeeY=35;
            _profile.AmountNumX=140; _profile.AmountNumY=35;
            _profile.AmountWordsX=25; _profile.AmountWordsY=50;
            _profile.ChequeNumX=25; _profile.ChequeNumY=65;
            BuildCanvas();
        }

        static ChequeProfile Clone(ChequeProfile p) => new()
        {
            DateX=p.DateX,DateY=p.DateY,PayeeX=p.PayeeX,PayeeY=p.PayeeY,
            AmountNumX=p.AmountNumX,AmountNumY=p.AmountNumY,
            AmountWordsX=p.AmountWordsX,AmountWordsY=p.AmountWordsY,
            ChequeNumX=p.ChequeNumX,ChequeNumY=p.ChequeNumY
        };
    }
}
