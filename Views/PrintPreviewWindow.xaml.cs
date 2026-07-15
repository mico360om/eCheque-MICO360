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
                TxtVersion.Text = "v" + Helpers.AppInfo.Version;
                RenderPreview();
                GeneratePdf();
                UpdatePaperCheck();
            };
        }

        // ───────────── remembered printer + paper-size trust check ─────────────

        /// <summary>This PC's remembered cheque printer (device-local setting; never synced).</summary>
        private static string RememberedPrinter
        {
            get => DatabaseService.GetSetting(eCheque.MICO360.Helpers.PrintHelper.LocalPrinterKey, "");
            set => DatabaseService.SaveSetting(eCheque.MICO360.Helpers.PrintHelper.LocalPrinterKey, value);
        }

        /// <summary>Shows, BEFORE any cheque is used, whether the remembered printer will honour the cheque's
        /// page size — the single biggest source of misprinted cheques (silent A4 substitution).</summary>
        private void UpdatePaperCheck()
        {
            try
            {
                var name = RememberedPrinter;
                if (string.IsNullOrEmpty(name))
                {
                    TxtPaperCheck.Text = $"No cheque printer remembered on this PC — Print Now will ask. Template: {_profile.ChequeWidth:0}×{_profile.ChequeHeight:0} mm.";
                    TxtPaperCheck.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
                    return;
                }
                var queue = eCheque.MICO360.Helpers.PrintHelper.FindQueue(name);
                if (queue == null)
                {
                    TxtPaperCheck.Text = $"⚠ Remembered printer \"{name}\" was not found (renamed or offline) — Print Now will ask.";
                    TxtPaperCheck.Foreground = new SolidColorBrush(Color.FromRgb(0xB2, 0x6A, 0x00));
                    return;
                }
                var r = eCheque.MICO360.Helpers.PrintHelper.ResolveForQueue(queue, _profile.ChequeWidth, _profile.ChequeHeight);
                if (r.Matched)
                {
                    TxtPaperCheck.Text = $"✓ {name} — will print on the cheque size {r.Wmm:0}×{r.Hmm:0} mm.";
                    TxtPaperCheck.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                }
                else
                {
                    TxtPaperCheck.Text = $"⚠ {name} — cannot use {_profile.ChequeWidth:0}×{_profile.ChequeHeight:0} mm; it will substitute {r.Wmm:0}×{r.Hmm:0} mm. Content still prints at actual size (top-left), but verify with a plain-paper test first.";
                    TxtPaperCheck.Foreground = new SolidColorBrush(Color.FromRgb(0xB2, 0x6A, 0x00));
                }
            }
            catch { TxtPaperCheck.Text = ""; }
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

            var dlg = PreparePrintDialog(rememberChoice: true);
            if (dlg == null) return;
            // Force + validate the cheque page size against the printer that will be used.
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

        /// <summary>
        /// Returns a ready PrintDialog, or null if the user cancelled. Uses this PC's remembered cheque printer
        /// (one quick "leaf loaded?" confirm — no printer dialog) when available; otherwise shows the dialog and
        /// remembers the chosen printer for next time. Cuts the daily flow to a single deliberate confirmation
        /// while keeping the leaf-insertion checkpoint for shared printers.
        /// </summary>
        private PrintDialog? PreparePrintDialog(bool rememberChoice, string? loadPrompt = null)
        {
            var name = RememberedPrinter;
            var queue = eCheque.MICO360.Helpers.PrintHelper.FindQueue(name);
            if (queue != null)
            {
                if (MessageBox.Show(
                        (loadPrompt ?? $"Print cheque #{_cheque.ChequeNumber} on \"{name}\"?\n\nMake sure the cheque leaf is loaded in the printer.")
                        + "\n(Use the Printer… button to change printers.)",
                        "Ready to Print", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return null;
                var quick = new PrintDialog();
                try { quick.PrintQueue = queue; return quick; }
                catch { /* stale queue — fall through to the dialog */ }
            }

            var dlg = new PrintDialog();
            // Hint the cheque-sized page before the dialog opens (SelectChequeMedia after makes it stick).
            eCheque.MICO360.Helpers.PrintHelper.ApplyChequeMedia(dlg, _profile.ChequeWidth, _profile.ChequeHeight);
            if (dlg.ShowDialog() != true) return null;
            if (rememberChoice && dlg.PrintQueue != null)
            {
                RememberedPrinter = dlg.PrintQueue.FullName;
                UpdatePaperCheck();
            }
            return dlg;
        }

        /// <summary>Choose (and remember) this PC's cheque printer without printing anything.</summary>
        private void BtnChoosePrinter_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PrintDialog();
            eCheque.MICO360.Helpers.PrintHelper.ApplyChequeMedia(dlg, _profile.ChequeWidth, _profile.ChequeHeight);
            if (dlg.ShowDialog() != true || dlg.PrintQueue == null) return;
            RememberedPrinter = dlg.PrintQueue.FullName;
            UpdatePaperCheck();
        }

        /// <summary>Prints the alignment grid (rulers + field crosshairs) on PLAIN paper so the user can hold it
        /// against a real leaf and trust the alignment before consuming a cheque. Never marks the cheque printed.</summary>
        private void BtnTestPrint_Click(object sender, RoutedEventArgs e)
        {
            var dlg = PreparePrintDialog(rememberChoice: true,
                loadPrompt: $"Print the alignment test sheet on \"{RememberedPrinter}\"?\n\nLoad PLAIN PAPER (not a cheque) in the printer.");
            if (dlg == null) return;
            var resolved = eCheque.MICO360.Helpers.PrintHelper.SelectChequeMedia(dlg, _profile.ChequeWidth, _profile.ChequeHeight);

            const double pxPerMm = 96.0 / 25.4;
            double cw = _profile.ChequeWidth * pxPerMm, ch = _profile.ChequeHeight * pxPerMm;
            var canvas = CalibrationRenderer.Build(_profile, pxPerMm);
            eCheque.MICO360.Helpers.PrintHelper.PrintActualSize(dlg, canvas, cw, ch, resolved.Wdip, resolved.Hdip, $"Alignment test — {_profile.Name}");
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "SYSTEM",
                "Alignment Test Print", _profile.Name, $"On {dlg.PrintQueue?.FullName} ({resolved.Wmm:0}×{resolved.Hmm:0} mm)");
            ToastService.Info("Alignment sheet sent — hold it against a cheque leaf to verify positions.");
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
