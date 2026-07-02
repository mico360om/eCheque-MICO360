using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.Views
{
    public partial class ChequeDesignerWindow : Window
    {
        readonly ChequeProfile _profile = null!;
        readonly string _origImage = "", _origFields = "";
        List<ChequeField> _fields = new();
        ChequeField? _selected;
        double _scale = 96.0 / 25.4 * 2.0;   // ~200%
        bool _suppress;

        // drag state
        Border? _dragBox; ChequeField? _dragField; Point _dragOffset;

        static readonly (string Key, string Label)[] Addable =
        {
            ("AccountNumber","Account Number"), ("ChequeNumber","Cheque Number"),
            ("Signature","Signature line"), ("Custom","Custom field…")
        };

        public bool Saved { get; private set; }

        public ChequeDesignerWindow() => InitializeComponent();

        public ChequeDesignerWindow(ChequeProfile profile) : this()
        {
            _profile = profile;
            _origImage = profile.BackgroundImage ?? "";
            _origFields = profile.FieldsJson ?? "";
            _fields = ChequeLayout.Parse(profile);

            TxtProfile.Text = $"{profile.Name} — {profile.BankName}  ({profile.ChequeWidth:0} × {profile.ChequeHeight:0} mm)";
            CmbAdd.ItemsSource = Addable.Select(a => a.Label).ToList();
            CmbAdd.SelectedIndex = 0;
            LstFields.ItemsSource = _fields;

            LoadBg();
            Loaded += (_, _) => AfterZoom();   // builds the canvas once sized
        }

        // ── background ──
        void LoadBg()
        {
            if (string.IsNullOrWhiteSpace(_profile.BackgroundImage)) { BgImage.Source = null; return; }
            try { BgImage.Source = new Bitmap(new MemoryStream(Convert.FromBase64String(_profile.BackgroundImage))); }
            catch { BgImage.Source = null; }
        }

        async void OnUpload(object? sender, RoutedEventArgs e)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select a scanned cheque image",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" } } }
            });
            if (files.Count == 0) return;
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                if (bytes.Length > 8 * 1024 * 1024) { TxtPosition.Text = "Image is too large (max 8 MB)."; return; }
                _profile.BackgroundImage = Convert.ToBase64String(bytes);
                LoadBg(); BuildCanvas();
                TxtPosition.Text = "Cheque image loaded. Drag each field onto the correct spot.";
            }
            catch (Exception ex) { TxtPosition.Text = "Could not load image: " + ex.Message; }
        }

        void OnRemoveImage(object? sender, RoutedEventArgs e)
        {
            _profile.BackgroundImage = "";
            BgImage.Source = null;
            BuildCanvas();
        }

        // ── canvas ──
        void BuildCanvas()
        {
            FieldCanvas.Children.Clear();
            double w = _profile.ChequeWidth * _scale, h = _profile.ChequeHeight * _scale;
            FieldCanvas.Width = w; FieldCanvas.Height = h;
            BgImage.Width = w; BgImage.Height = h;
            foreach (var f in _fields) AddFieldBox(f);
        }

        void AddFieldBox(ChequeField f)
        {
            var col = ColorFor(f.Key);
            var box = new Border
            {
                Tag = f,
                Background = new SolidColorBrush(col, f.Enabled ? 0.20 : 0.08),
                BorderBrush = new SolidColorBrush(col),
                BorderThickness = new Thickness(ReferenceEquals(_selected, f) ? 2.4 : 1.4),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 3, 6, 3),
                Cursor = new Cursor(StandardCursorType.SizeAll),
                Child = new TextBlock { Text = (f.Enabled ? "" : "○ ") + f.Label, Foreground = new SolidColorBrush(col), FontWeight = FontWeight.SemiBold, FontSize = 11 }
            };
            Canvas.SetLeft(box, f.X * _scale);
            Canvas.SetTop(box, f.Y * _scale);
            box.PointerPressed += OnFieldPressed;
            FieldCanvas.Children.Add(box);
        }

        static Color ColorFor(string key) => Color.Parse(key switch
        {
            "Date" => "#1565C0", "Payee" => "#2E7D32", "AmountNum" => "#E65100",
            "AmountWords" => "#6A1B9A", "AccountNumber" => "#00838F", "ChequeNumber" => "#8B1818",
            "Signature" => "#455A64", _ => "#5D4037"
        });

        // ── drag ──
        void OnFieldPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border b && b.Tag is ChequeField f)
            {
                _dragBox = b; _dragField = f;
                var p = e.GetPosition(FieldCanvas);
                _dragOffset = new Point(p.X - Canvas.GetLeft(b), p.Y - Canvas.GetTop(b));
                e.Pointer.Capture(FieldCanvas);
                Select(f);
                e.Handled = true;
            }
        }

        void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragBox == null || _dragField == null) return;
            var p = e.GetPosition(FieldCanvas);
            double nx = Math.Max(0, Math.Min(p.X - _dragOffset.X, FieldCanvas.Width - _dragBox.Bounds.Width));
            double ny = Math.Max(0, Math.Min(p.Y - _dragOffset.Y, FieldCanvas.Height - _dragBox.Bounds.Height));
            Canvas.SetLeft(_dragBox, nx); Canvas.SetTop(_dragBox, ny);
            TxtPosition.Text = $"{_dragField.Label}:  X = {nx / _scale:F1} mm,  Y = {ny / _scale:F1} mm";
        }

        void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragBox == null || _dragField == null) return;
            _dragField.X = Math.Round(Canvas.GetLeft(_dragBox) / _scale, 1);
            _dragField.Y = Math.Round(Canvas.GetTop(_dragBox) / _scale, 1);
            e.Pointer.Capture(null);
            _dragBox = null; _dragField = null;
            PopulateProps();
        }

        // ── selection / properties ──
        void Select(ChequeField f)
        {
            _selected = f;
            if (!ReferenceEquals(LstFields.SelectedItem, f)) LstFields.SelectedItem = f;
            PanelProps.IsEnabled = true;
            PopulateProps();
            RefreshSelectionVisual();
        }

        void RefreshSelectionVisual()
        {
            foreach (var c in FieldCanvas.Children)
                if (c is Border b && b.Tag is ChequeField f)
                    b.BorderThickness = new Thickness(ReferenceEquals(f, _selected) ? 2.4 : 1.4);
        }

        void OnFieldSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (LstFields.SelectedItem is ChequeField f && !ReferenceEquals(f, _selected)) Select(f);
        }

        void PopulateProps()
        {
            if (_selected == null) return;
            _suppress = true;
            ChkEnabled.IsChecked = _selected.Enabled;
            TxtLabel.Text = _selected.IsCustom ? _selected.CustomText : _selected.Label;
            TxtSize.Text = _selected.FontSize.ToString("0.#");
            TxtWidth.Text = _selected.Width.ToString("0.#");
            TxtX.Text = _selected.X.ToString("0.#");
            TxtY.Text = _selected.Y.ToString("0.#");
            CmbAlign.SelectedIndex = _selected.Align switch { "Center" => 1, "Right" => 2, _ => 0 };
            ChkBold.IsChecked = _selected.Bold;
            _suppress = false;
        }

        void OnPropChanged(object? sender, RoutedEventArgs e)
        {
            if (_suppress || _selected == null) return;
            _selected.Enabled = ChkEnabled.IsChecked == true;
            if (_selected.IsCustom)
            {
                _selected.CustomText = TxtLabel.Text ?? "";
                _selected.Label = string.IsNullOrWhiteSpace(TxtLabel.Text) ? "Custom" : TxtLabel.Text!;
            }
            if (double.TryParse(TxtSize.Text, out var sz) && sz > 0) _selected.FontSize = sz;
            if (double.TryParse(TxtWidth.Text, out var wd) && wd > 0) _selected.Width = wd;
            if (double.TryParse(TxtX.Text, out var xx)) _selected.X = xx;
            if (double.TryParse(TxtY.Text, out var yy)) _selected.Y = yy;
            _selected.Align = CmbAlign.SelectedIndex switch { 1 => "Center", 2 => "Right", _ => "Left" };
            _selected.Bold = ChkBold.IsChecked == true;
            BuildCanvas();
        }

        // ── add / delete ──
        void OnAddField(object? sender, RoutedEventArgs e)
        {
            int i = CmbAdd.SelectedIndex; if (i < 0) i = 0;
            var (key, label) = Addable[i];
            ChequeField f;
            if (key == "Custom")
                f = new ChequeField { Key = "Custom", Label = "Custom", IsCustom = true, CustomText = "Text", X = 20, Y = 20, Enabled = true };
            else
            {
                if (_fields.Any(x => x.Key == key)) { TxtPosition.Text = $"{label} is already on the cheque."; return; }
                f = new ChequeField { Key = key, Label = label, X = 20, Y = 20, Enabled = true };
            }
            _fields.Add(f);
            LstFields.ItemsSource = null; LstFields.ItemsSource = _fields;
            Select(f);
            BuildCanvas();
        }

        void OnDeleteField(object? sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            _fields.Remove(_selected);
            _selected = null;
            PanelProps.IsEnabled = false;
            LstFields.ItemsSource = null; LstFields.ItemsSource = _fields;
            BuildCanvas();
        }

        // ── zoom ──
        void OnZoomIn(object? sender, RoutedEventArgs e) { _scale = Math.Min(10, _scale + 0.8); AfterZoom(); }
        void OnZoomOut(object? sender, RoutedEventArgs e) { _scale = Math.Max(1.0, _scale - 0.8); AfterZoom(); }
        void AfterZoom() { TxtZoom.Text = $"{_scale / (96.0 / 25.4) * 100:0}%"; BuildCanvas(); }

        // ── actions ──
        void OnReset(object? sender, RoutedEventArgs e)
        {
            _fields = ChequeLayout.Default(_profile);
            _selected = null; PanelProps.IsEnabled = false;
            LstFields.ItemsSource = _fields;
            BuildCanvas();
        }

        void OnSave(object? sender, RoutedEventArgs e)
        {
            SyncLegacy();
            _profile.FieldsJson = ChequeLayout.Serialize(_fields);
            ChequeService.SaveLayout(_profile);
            Saved = true;
            Close();
        }

        void OnCancel(object? sender, RoutedEventArgs e)
        {
            _profile.BackgroundImage = _origImage;
            _profile.FieldsJson = _origFields;
            Close();
        }

        // keep the legacy per-field X/Y columns in sync for the print fallback
        void SyncLegacy()
        {
            void S(string key, ref double x, ref double y) { var f = _fields.FirstOrDefault(z => z.Key == key); if (f != null) { x = f.X; y = f.Y; } }
            double dx = _profile.DateX, dy = _profile.DateY; S("Date", ref dx, ref dy); _profile.DateX = dx; _profile.DateY = dy;
            double px = _profile.PayeeX, py = _profile.PayeeY; S("Payee", ref px, ref py); _profile.PayeeX = px; _profile.PayeeY = py;
            double ax = _profile.AmountNumX, ay = _profile.AmountNumY; S("AmountNum", ref ax, ref ay); _profile.AmountNumX = ax; _profile.AmountNumY = ay;
            double wx = _profile.AmountWordsX, wy = _profile.AmountWordsY; S("AmountWords", ref wx, ref wy); _profile.AmountWordsX = wx; _profile.AmountWordsY = wy;
            double nx = _profile.ChequeNumX, ny = _profile.ChequeNumY; S("ChequeNumber", ref nx, ref ny); _profile.ChequeNumX = nx; _profile.ChequeNumY = ny;
        }
    }
}
