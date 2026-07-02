using System.Windows;
using System.Windows.Controls;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using FontFamily = System.Windows.Media.FontFamily;
using VisualBrush = System.Windows.Media.VisualBrush;
using DrawingVisual = System.Windows.Media.DrawingVisual;
using RenderTargetBitmap = System.Windows.Media.Imaging.RenderTargetBitmap;
using PixelFormats = System.Windows.Media.PixelFormats;
using Rectangle = System.Windows.Shapes.Rectangle;
using Line = System.Windows.Shapes.Line;
using Size = System.Windows.Size;
using PrintDialog = System.Windows.Controls.PrintDialog;

namespace eCheque.MICO360.Views
{
    public partial class PrintPreviewWindow : Window
    {
        private readonly ChequeRecord _cheque;
        private readonly ChequeProfile _profile;
        public bool WasPrinted { get; private set; }
        public string PdfPath { get; private set; } = "";

        public PrintPreviewWindow(ChequeRecord cheque, ChequeProfile profile)
        {
            InitializeComponent();
            _cheque = cheque;
            _profile = profile;
            Loaded += (s, e) =>
            {
                TxtInfo.Text = $"Cheque #{cheque.ChequeNumber}  |  {cheque.PayeeName}  |  {cheque.Currency} {cheque.Amount:N3}";
                RenderPreview();
                GeneratePdf();
            };
        }

        private Canvas BuildChequeCanvas(double w, double h, bool includeBackground = false)
        {
            const double pxPerMm = 96.0 / 25.4;
            // New visual layout: render only the entered data at the saved field positions (plus the
            // scanned template on screen only). Falls back to the classic decorative cheque for old profiles.
            if (ChequeRenderer.HasLayout(_profile))
                return ChequeRenderer.Build(_profile, _cheque, pxPerMm, includeBackground);

            var canvas = new Canvas { Width = w, Height = h, Background = Brushes.White, ClipToBounds = true };

            // Header band
            var header = new Rectangle { Width = w, Height = 9 * pxPerMm, Fill = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) };
            Canvas.SetLeft(header, 0); Canvas.SetTop(header, 0);
            canvas.Children.Add(header);

            // Bank name in header
            canvas.Children.Add(MakeTb(_profile.BankName + "    A/C: " + _cheque.AccountNumber,
                _profile.FontFamily, 8, true, Colors.Black, 5, 1.5));

            // Border
            var border = new Rectangle { Width = w - 1, Height = h - 1,
                Stroke = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                StrokeThickness = 1, Fill = Brushes.Transparent };
            Canvas.SetLeft(border, 0.5); Canvas.SetTop(border, 0.5);
            canvas.Children.Add(border);

            // MICR zone
            var micr = new Rectangle { Width = w, Height = 12 * pxPerMm,
                Fill = new SolidColorBrush(Color.FromRgb(0xF8, 0xF0, 0xF0)) };
            Canvas.SetLeft(micr, 0); Canvas.SetTop(micr, h - 12 * pxPerMm);
            canvas.Children.Add(micr);

            // Signature line
            var sig = new Line { X1 = w - 52 * pxPerMm, Y1 = h - 12 * pxPerMm,
                X2 = w - 4 * pxPerMm, Y2 = h - 12 * pxPerMm,
                Stroke = Brushes.Gray, StrokeThickness = 0.8 };
            canvas.Children.Add(sig);
            canvas.Children.Add(MakeTb("Authorized Signature", _profile.FontFamily, 7, false,
                Color.FromRgb(0x88, 0x88, 0x88), w - 52 * pxPerMm, h - 9.5 * pxPerMm));

            // Field helper
            void AddField(string label, string value, double xMm, double yMm)
            {
                canvas.Children.Add(MakeTb(label, _profile.FontFamily, 7, false,
                    Color.FromRgb(0x88, 0x88, 0x88), xMm * pxPerMm, (yMm - 4) * pxPerMm));
                canvas.Children.Add(MakeTb(value, _profile.FontFamily, _profile.FontSize,
                    _profile.IsBold, Colors.Black, xMm * pxPerMm, yMm * pxPerMm));
                var ul = new Line { X1 = xMm * pxPerMm, Y1 = (yMm + 5) * pxPerMm,
                    X2 = Math.Min((xMm + 80) * pxPerMm, w - 5 * pxPerMm), Y2 = (yMm + 5) * pxPerMm,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)), StrokeThickness = 0.5 };
                canvas.Children.Add(ul);
            }

            AddField("Date:", _cheque.ChequeDate.ToString("dd / MM / yyyy"), _profile.DateX, _profile.DateY);
            AddField("Pay to:", _cheque.PayeeName, _profile.PayeeX, _profile.PayeeY);
            AddField("Amount:", $"{_cheque.Currency} {_cheque.Amount:N3}", _profile.AmountNumX, _profile.AmountNumY);
            AddField("In words:", _cheque.AmountInWords, _profile.AmountWordsX, _profile.AmountWordsY);
            AddField("Cheque No:", _cheque.ChequeNumber, _profile.ChequeNumX, _profile.ChequeNumY);

            return canvas;
        }

        private void RenderPreview()
        {
            try
            {
                const double dpi = 96;
                const double pxPerMm = dpi / 25.4;
                double w = _profile.ChequeWidth  * pxPerMm;
                double h = _profile.ChequeHeight * pxPerMm;

                var canvas = BuildChequeCanvas(w, h, includeBackground: true);
                canvas.Measure(new Size(w, h));
                canvas.Arrange(new Rect(0, 0, w, h));
                canvas.UpdateLayout();

                // Render at 1.5× for crisp screen preview
                var rtb = new RenderTargetBitmap((int)(w * 1.5), (int)(h * 1.5), dpi * 1.5, dpi * 1.5, PixelFormats.Pbgra32);
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                {
                    var vb = new VisualBrush(canvas);
                    dc.DrawRectangle(vb, null, new Rect(0, 0, w * 1.5, h * 1.5));
                }
                rtb.Render(dv);
                PreviewImage.Source = rtb;
                PreviewImage.Width  = w * 1.5;
                PreviewImage.Height = h * 1.5;
            }
            catch (Exception ex)
            {
                TxtInfo.Text += $"   [Preview error: {ex.Message}]";
            }
        }

        private static TextBlock MakeTb(string text, string font, double size, bool bold,
            Color color, double x, double y)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontFamily = new FontFamily(font),
                FontSize = size,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(color),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 200
            };
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
            return tb;
        }

        private void GeneratePdf()
        {
            try
            {
                PdfPath = PdfService.GeneratePdf(_cheque, _profile);
                TxtPdfPath.Text = $"PDF ready: {System.IO.Path.GetFileName(PdfPath)}";
            }
            catch (Exception ex)
            {
                TxtPdfPath.Text = $"PDF error: {ex.Message}";
            }
        }

        private void BtnSavePdf_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PdfPath)) GeneratePdf();
            if (!string.IsNullOrEmpty(PdfPath))
            {
                MessageBox.Show($"PDF saved to:\n{PdfPath}", "PDF Saved", MessageBoxButton.OK, MessageBoxImage.Information);
                PdfService.Open(PdfPath);
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Defense in depth: never print a cancelled or voided cheque even if this
            // window is opened outside the main print flow.
            if (ChequeService.IsPrintBlocked(_cheque.Status))
            {
                MessageBox.Show($"Cheque #{_cheque.ChequeNumber} is {_cheque.Status} and cannot be printed.",
                    "Print Blocked", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new PrintDialog();
            // Print on a page sized to the cheque itself (not a default A4 sheet).
            eCheque.MICO360.Helpers.PrintHelper.ApplyChequeMedia(dlg, _profile.ChequeWidth, _profile.ChequeHeight);
            if (dlg.ShowDialog() != true) return;

            const double pxPerMm = 96.0 / 25.4;
            double cw = _profile.ChequeWidth  * pxPerMm;
            double ch = _profile.ChequeHeight * pxPerMm;

            // Build a fresh canvas for the printer and print it at true 1:1 size (top-left) so the
            // text lands where it should on a real cheque; only shrink if the cheque exceeds the page.
            var canvas = BuildChequeCanvas(cw, ch);
            eCheque.MICO360.Helpers.PrintHelper.PrintActualSize(dlg, canvas, cw, ch, $"Cheque #{_cheque.ChequeNumber} — eCheque MICO360");

            WasPrinted = true;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "SYSTEM",
                "Print", _cheque.ChequeNumber, $"Printed to {dlg.PrintQueue?.FullName}");
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
