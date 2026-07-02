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
            => ChequePrintBuilder.Build(_cheque, _profile, w, h, includeBackground);

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
            // Hint the cheque-sized page before the dialog opens...
            eCheque.MICO360.Helpers.PrintHelper.ApplyChequeMedia(dlg, _profile.ChequeWidth, _profile.ChequeHeight);
            if (dlg.ShowDialog() != true) return;
            // ...then force + validate it against the printer the user actually chose (this is what makes it stick).
            var resolved = eCheque.MICO360.Helpers.PrintHelper.SelectChequeMedia(dlg, _profile.ChequeWidth, _profile.ChequeHeight);

            // If the printer couldn't use the cheque size, warn before wasting a cheque.
            if (!resolved.Matched &&
                MessageBox.Show(
                    $"Your printer could not use the cheque page size ({_profile.ChequeWidth:0}×{_profile.ChequeHeight:0} mm) and will use {resolved.Wmm:0}×{resolved.Hmm:0} mm instead.\n\nThe cheque will still print at actual size in the top-left, but alignment on a real cheque may be off. Set a custom paper size of {_profile.ChequeWidth:0}×{_profile.ChequeHeight:0} mm in your printer for best results.\n\nContinue printing?",
                    "Printer Page Size", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            const double pxPerMm = 96.0 / 25.4;
            double cw = _profile.ChequeWidth  * pxPerMm;
            double ch = _profile.ChequeHeight * pxPerMm;

            // Build a fresh canvas for the printer and print it at true 1:1 size (top-left) so the
            // text lands where it should on a real cheque; only shrink if the cheque exceeds the page.
            var canvas = BuildChequeCanvas(cw, ch);
            eCheque.MICO360.Helpers.PrintHelper.PrintActualSize(dlg, canvas, cw, ch, resolved.Wdip, resolved.Hdip, $"Cheque #{_cheque.ChequeNumber} — eCheque MICO360");

            WasPrinted = true;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "SYSTEM",
                "Print", _cheque.ChequeNumber, $"Printed to {dlg.PrintQueue?.FullName} on {resolved.Wmm:0}×{resolved.Hmm:0} mm page");
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
