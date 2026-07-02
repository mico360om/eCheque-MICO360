using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using Key = System.Windows.Input.Key;
using Keyboard = System.Windows.Input.Keyboard;
using TextBox = System.Windows.Controls.TextBox;
using Cursors = System.Windows.Input.Cursors;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Brushes = System.Windows.Media.Brushes;
using DoubleCollection = System.Windows.Media.DoubleCollection;
using Line = System.Windows.Shapes.Line;
using Panel = System.Windows.Controls.Panel;
using Thickness = System.Windows.Thickness;
using TextBlock = System.Windows.Controls.TextBlock;
using Border = System.Windows.Controls.Border;
using ComboBox = System.Windows.Controls.ComboBox;
using CheckBox = System.Windows.Controls.CheckBox;
using PrintDialog = System.Windows.Controls.PrintDialog;
using Viewbox = System.Windows.Controls.Viewbox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace eCheque.MICO360.Views
{
    public partial class ChequeLayoutDesigner : Window
    {
        readonly ChequeProfile _profile;
        string _originalFields = "", _originalImage = "";
        List<ChequeField> _fields = new();
        ChequeField? _selected;
        Border? _selBox;
        double _scale = 3.2;
        bool _dragging, _suppress, _userZoomed;
        Border? _dragEl; Point _dragOffset;

        static readonly string[] FontChoices = { "Arial", "Times New Roman", "Calibri", "Courier New", "Verdana", "Tahoma", "Segoe UI" };

        static readonly (string Key, string Label)[] Addable =
        {
            ("AccountNumber", "Account Number"), ("ChequeNumber", "Cheque Number"),
            ("Signature", "Signature line"), ("Custom", "Custom field…")
        };

        public bool Saved { get; private set; }

        public ChequeLayoutDesigner(ChequeProfile profile)
        {
            InitializeComponent();
            _profile = profile;
            try
            {
                _fields = ChequeLayout.Parse(profile);
                _originalFields = ChequeLayout.Serialize(_fields);
                _originalImage = profile.BackgroundImage ?? "";

                TxtProfileName.Text = $"{profile.Name} — {profile.BankName}  ({profile.ChequeWidth:N0} × {profile.ChequeHeight:N0} mm)";
                foreach (var f in FontChoices) CmbFont.Items.Add(f);
                foreach (var a in Addable) CmbAddField.Items.Add(new ComboBoxItem { Content = a.Label, Tag = a.Key });
                CmbAddField.SelectedIndex = 0;
                LstFields.ItemsSource = _fields;
            }
            catch (Exception ex) { BugReportService.Report(ex, "ChequeLayoutDesigner.ctor"); }

            KeyDown += Window_KeyDown;

            Loaded += (s, e) =>
            {
                try { LoadBackground(); BuildCanvas(); FitToWindow(); }
                catch (Exception ex)
                {
                    BugReportService.Report(ex, "ChequeLayoutDesigner.Loaded");
                    MessageBox.Show($"The layout designer hit an error:\n\n{ex.Message}\n\n(Logged for the developers.)",
                        "Design Layout", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
        }

        // Press Delete to remove the selected field (unless typing in a text box).
        void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete && _selected != null && Keyboard.FocusedElement is not TextBox)
            {
                BtnDeleteField_Click(sender, e);
                e.Handled = true;
            }
        }

        // ── Background image ──
        void LoadBackground() => BgImage.Source = ChequeRenderer.DecodeImage(_profile.BackgroundImage);

        void BtnUpload_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp", Title = "Select a scanned cheque image" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var bytes = System.IO.File.ReadAllBytes(dlg.FileName);
                if (bytes.Length > 4_000_000) { MessageBox.Show("Please use an image under 4 MB.", "Image too large"); return; }
                _profile.BackgroundImage = Convert.ToBase64String(bytes);
                LoadBackground();
                BuildCanvas();
                TxtPosition.Text = "Cheque image loaded. Drag each field onto the correct spot.";
            }
            catch (Exception ex) { MessageBox.Show($"Could not load image: {ex.Message}", "Error"); }
        }

        void BtnRemoveImage_Click(object sender, RoutedEventArgs e)
        {
            _profile.BackgroundImage = "";
            BgImage.Source = null;
            BuildCanvas();
        }

        // ── Canvas ──
        void BuildCanvas()
        {
            ChequeCanvas.Children.Clear();
            double w = _profile.ChequeWidth * _scale, h = _profile.ChequeHeight * _scale;
            ChequeCanvas.Width = w; ChequeCanvas.Height = h;
            BgImage.Width = w; BgImage.Height = h;

            if (ChkGrid.IsChecked == true)
            {
                for (int x = 0; x <= (int)_profile.ChequeWidth; x += 10)
                {
                    ChequeCanvas.Children.Add(new Line { X1 = x * _scale, Y1 = 0, X2 = x * _scale, Y2 = h, Stroke = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)), StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 2, 4 } });
                    if (x > 0) { var lbl = new TextBlock { Text = $"{x}", FontSize = 7, Foreground = new SolidColorBrush(Color.FromArgb(0x99, 0, 0, 0)) }; Canvas.SetLeft(lbl, x * _scale + 1); Canvas.SetTop(lbl, 1); ChequeCanvas.Children.Add(lbl); }
                }
                for (int y = 0; y <= (int)_profile.ChequeHeight; y += 10)
                    ChequeCanvas.Children.Add(new Line { X1 = 0, Y1 = y * _scale, X2 = w, Y2 = y * _scale, Stroke = new SolidColorBrush(Color.FromArgb(0x22, 0, 0, 0)), StrokeThickness = 0.5, StrokeDashArray = new DoubleCollection { 2, 4 } });
            }

            foreach (var f in _fields) AddFieldBox(f);
        }

        void AddFieldBox(ChequeField f)
        {
            var col = ColorFor(f.Key);
            var box = new Border
            {
                Tag = f,
                Background = new SolidColorBrush(Color.FromArgb(f.Enabled ? (byte)0x30 : (byte)0x14, col.R, col.G, col.B)),
                BorderBrush = new SolidColorBrush(f.Enabled ? col : Color.FromArgb(0x88, col.R, col.G, col.B)),
                BorderThickness = new Thickness(_selected == f ? 2.2 : 1.4),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = Cursors.SizeAll
            };
            box.Child = new TextBlock { Text = (f.Enabled ? "" : "○ ") + f.Label, Foreground = new SolidColorBrush(col), FontWeight = FontWeights.SemiBold, FontSize = 11 };
            Canvas.SetLeft(box, f.X * _scale); Canvas.SetTop(box, f.Y * _scale);
            Panel.SetZIndex(box, _selected == f ? 100 : 10);
            box.MouseLeftButtonDown += Field_MouseDown;
            ChequeCanvas.Children.Add(box);
            if (_selected == f) _selBox = box;
        }

        static Color ColorFor(string key) => (Color)ColorConverter.ConvertFromString(key switch
        {
            "Date" => "#1565C0", "Payee" => "#2E7D32", "AmountNum" => "#E65100",
            "AmountWords" => "#6A1B9A", "AccountNumber" => "#00838F", "ChequeNumber" => "#8B1818",
            "Signature" => "#455A64", _ => "#5D4037"
        });

        // ── Drag ──
        void Field_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragEl = (Border)sender;
            _dragging = true;
            _dragOffset = e.GetPosition(_dragEl);
            _dragEl.CaptureMouse();
            Select((ChequeField)_dragEl.Tag);
            e.Handled = true;
        }

        void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _dragEl == null) return;
            var pos = e.GetPosition(ChequeCanvas);
            _dragEl.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var nx = Math.Max(0, Math.Min(pos.X - _dragOffset.X, ChequeCanvas.Width - _dragEl.DesiredSize.Width));
            var ny = Math.Max(0, Math.Min(pos.Y - _dragOffset.Y, ChequeCanvas.Height - _dragEl.DesiredSize.Height));
            Canvas.SetLeft(_dragEl, nx); Canvas.SetTop(_dragEl, ny);
            TxtPosition.Text = $"{((ChequeField)_dragEl.Tag).Label}:  X = {nx / _scale:F1} mm,  Y = {ny / _scale:F1} mm";
        }

        void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging || _dragEl == null) return;
            _dragging = false;
            _dragEl.ReleaseMouseCapture();
            var f = (ChequeField)_dragEl.Tag;
            f.X = Math.Round(Canvas.GetLeft(_dragEl) / _scale, 1);
            f.Y = Math.Round(Canvas.GetTop(_dragEl) / _scale, 1);
            _dragEl = null;
            PopulateProps();
            TxtPosition.Text = $"{f.Label} placed at  X = {f.X:F1} mm,  Y = {f.Y:F1} mm  ✓";
        }

        // ── Selection + properties ──
        void Select(ChequeField f)
        {
            _selected = f;
            if (!ReferenceEquals(LstFields.SelectedItem, f)) LstFields.SelectedItem = f;
            PanelProps.IsEnabled = true;
            PopulateProps();
            // Update the highlight in place — do NOT rebuild the canvas here, or the box being
            // dragged would be recreated mid-drag (which is what made dragging stutter).
            RefreshSelectionVisual();
        }

        // Re-style existing field boxes to reflect the current selection without recreating them.
        void RefreshSelectionVisual()
        {
            _selBox = null;
            foreach (var child in ChequeCanvas.Children)
            {
                if (child is Border b && b.Tag is ChequeField f)
                {
                    bool sel = ReferenceEquals(f, _selected);
                    b.BorderThickness = new Thickness(sel ? 2.2 : 1.4);
                    Panel.SetZIndex(b, sel ? 100 : 10);
                    if (sel) _selBox = b;
                }
            }
        }

        void LstFields_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstFields.SelectedItem is ChequeField f && f != _selected) Select(f);
        }

        void PopulateProps()
        {
            if (_selected == null) return;
            _suppress = true;
            ChkEnabled.IsChecked = _selected.Enabled;
            TxtLabel.Text = _selected.IsCustom ? _selected.CustomText : _selected.Label;
            CmbFont.SelectedItem = FontChoices.Contains(_selected.FontFamily) ? _selected.FontFamily : "Arial";
            TxtSize.Text = _selected.FontSize.ToString("0.#");
            TxtWidth.Text = _selected.Width.ToString("0.#");
            TxtX.Text = _selected.X.ToString("0.#");
            TxtY.Text = _selected.Y.ToString("0.#");
            CmbAlign.SelectedIndex = _selected.Align switch { "Center" => 1, "Right" => 2, _ => 0 };
            ChkBold.IsChecked = _selected.Bold;
            BtnDeleteField.Visibility = Visibility.Visible;
            _suppress = false;
        }

        void Prop_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppress || _selected == null) return;
            _selected.Enabled = ChkEnabled.IsChecked == true;
            if (_selected.IsCustom) { _selected.CustomText = TxtLabel.Text; _selected.Label = string.IsNullOrWhiteSpace(TxtLabel.Text) ? "Custom" : TxtLabel.Text; }
            _selected.FontFamily = CmbFont.SelectedItem as string ?? "Arial";
            if (double.TryParse(TxtSize.Text, out var sz) && sz > 0) _selected.FontSize = sz;
            if (double.TryParse(TxtWidth.Text, out var wd) && wd > 0) _selected.Width = wd;
            if (double.TryParse(TxtX.Text, out var xx)) _selected.X = xx;
            if (double.TryParse(TxtY.Text, out var yy)) _selected.Y = yy;
            _selected.Align = CmbAlign.SelectedIndex switch { 1 => "Center", 2 => "Right", _ => "Left" };
            _selected.Bold = ChkBold.IsChecked == true;
            LstFields.Items.Refresh();
            BuildCanvas();
        }

        // ── Add / delete fields ──
        void BtnAddField_Click(object sender, RoutedEventArgs e)
        {
            if (CmbAddField.SelectedItem is not ComboBoxItem item) return;
            var key = item.Tag?.ToString() ?? "Custom";
            ChequeField f;
            if (key == "Custom")
                f = new ChequeField { Key = "Custom", Label = "Custom", IsCustom = true, CustomText = "Text", X = 20, Y = 20, Enabled = true, FontFamily = "Arial", FontSize = 11 };
            else
            {
                if (_fields.Any(x => x.Key == key)) { MessageBox.Show($"{item.Content} is already on the cheque.", "Field exists"); return; }
                var label = Addable.First(a => a.Key == key).Label;
                f = new ChequeField { Key = key, Label = label, X = 20, Y = 20, Enabled = true, FontFamily = "Arial", FontSize = 11 };
            }
            _fields.Add(f);
            LstFields.Items.Refresh();
            Select(f);
        }

        void BtnDeleteField_Click(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _fields.Remove(_selected);
            _selected = null; _selBox = null;
            PanelProps.IsEnabled = false;
            LstFields.Items.Refresh();
            BuildCanvas();
        }

        // ── Zoom / grid ──
        void BtnZoomIn_Click(object sender, RoutedEventArgs e) { _userZoomed = true; _scale = Math.Min(10, _scale + 0.6); AfterZoom(); }
        void BtnZoomOut_Click(object sender, RoutedEventArgs e) { _userZoomed = true; _scale = Math.Max(1.0, _scale - 0.6); AfterZoom(); }
        void BtnFit_Click(object sender, RoutedEventArgs e) => FitToWindow();
        void AfterZoom() { const double pxPerMm = 96.0 / 25.4; TxtZoom.Text = $"{_scale / pxPerMm * 100:0}%"; BuildCanvas(); }
        void ChkGrid_Click(object sender, RoutedEventArgs e) => BuildCanvas();

        // Scale the cheque so the whole thing fits inside the visible work area.
        void FitToWindow()
        {
            if (_profile.ChequeWidth <= 0 || _profile.ChequeHeight <= 0) return;
            double vw = CanvasScroller.ViewportWidth  > 0 ? CanvasScroller.ViewportWidth  : CanvasScroller.ActualWidth;
            double vh = CanvasScroller.ViewportHeight > 0 ? CanvasScroller.ViewportHeight : CanvasScroller.ActualHeight;
            vw -= 72; vh -= 72; // padding + border/margin breathing room
            if (vw <= 0 || vh <= 0) return;
            double s = Math.Min(vw / _profile.ChequeWidth, vh / _profile.ChequeHeight);
            _scale = Math.Max(1.0, Math.Min(10, s));
            _userZoomed = false;
            AfterZoom();
        }

        // Keep the cheque fitted while the window is resized — until the user manually zooms.
        void CanvasScroller_SizeChanged(object sender, SizeChangedEventArgs e) { if (!_userZoomed) FitToWindow(); }

        // ── Test print / save / cancel ──
        void SyncProfile()
        {
            // keep legacy X/Y in sync for the fallback print path
            void Sync(string key, ref double x, ref double y) { var f = _fields.FirstOrDefault(z => z.Key == key); if (f != null) { x = f.X; y = f.Y; } }
            double dx = _profile.DateX, dy = _profile.DateY; Sync("Date", ref dx, ref dy); _profile.DateX = dx; _profile.DateY = dy;
            double px = _profile.PayeeX, py = _profile.PayeeY; Sync("Payee", ref px, ref py); _profile.PayeeX = px; _profile.PayeeY = py;
            double ax = _profile.AmountNumX, ay = _profile.AmountNumY; Sync("AmountNum", ref ax, ref ay); _profile.AmountNumX = ax; _profile.AmountNumY = ay;
            double wx = _profile.AmountWordsX, wy = _profile.AmountWordsY; Sync("AmountWords", ref wx, ref wy); _profile.AmountWordsX = wx; _profile.AmountWordsY = wy;
            double nx = _profile.ChequeNumX, ny = _profile.ChequeNumY; Sync("ChequeNumber", ref nx, ref ny); _profile.ChequeNumX = nx; _profile.ChequeNumY = ny;
            _profile.FieldsJson = ChequeLayout.Serialize(_fields);
        }

        void BtnTestPrint_Click(object sender, RoutedEventArgs e)
        {
            SyncProfile();
            var dlg = new PrintDialog();
            eCheque.MICO360.Helpers.PrintHelper.ApplyMediaSize(dlg, _profile.PaperSize);
            if (dlg.ShowDialog() != true) return;
            const double pxPerMm = 96.0 / 25.4;
            var canvas = ChequeRenderer.Build(_profile, ChequeLayout.SampleCheque(_profile), pxPerMm, includeBackground: false);
            double cw = _profile.ChequeWidth * pxPerMm, ch = _profile.ChequeHeight * pxPerMm;
            eCheque.MICO360.Helpers.PrintHelper.PrintActualSize(dlg, canvas, cw, ch, "Cheque layout test print");
            TxtPosition.Text = "Test print sent at actual size. Hold the printout against a real cheque to verify alignment.";
        }

        void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SyncProfile();
            ChequeService.SaveLayout(_profile);
            Saved = true;
            Close();
        }

        void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _profile.BackgroundImage = _originalImage;
            _profile.FieldsJson = _originalFields;
            Close();
        }

        void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            _fields = ChequeLayout.Default(_profile);
            LstFields.ItemsSource = _fields;
            _selected = null; PanelProps.IsEnabled = false;
            BuildCanvas();
        }
    }
}
