using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using eCheque.MICO360.Core.Models;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace eCheque.MICO360.Mac.Services
{
    /// <summary>
    /// macOS cheque printing. WPF's System.Printing doesn't exist here, so we render the cheque data
    /// (positioned per the profile) to a crisp image, embed it in a PDF page sized to the cheque itself,
    /// and open it — the user prints from Preview (⌘P) at exact 1:1 size. Image-only PDF, so no font
    /// resolver is required (Avalonia handles the text rendering).
    /// </summary>
    public static class ChequePrintService
    {
        const double Dip = 96.0 / 25.4; // device-independent pixels per millimetre

        public static string GeneratePdf(ChequeRecord cheque, ChequeProfile profile)
        {
            double cw = Math.Max(1, profile.ChequeWidth)  * Dip;
            double ch = Math.Max(1, profile.ChequeHeight) * Dip;

            var visual = BuildVisual(cheque, profile, cw, ch);
            byte[] png = RenderPng(visual, cw, ch, 3.0); // 3× ≈ 288 dpi

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                   "eCheque MICO360", "PDFs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir,
                $"Cheque_{Safe(cheque.ChequeNumber)}_{cheque.ChequeDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");

            using var doc = new PdfDocument();
            doc.Info.Title = $"Cheque {cheque.ChequeNumber}";
            var page = doc.AddPage();
            page.Width  = XUnit.FromMillimeter(profile.ChequeWidth);
            page.Height = XUnit.FromMillimeter(profile.ChequeHeight);

            var ms = new MemoryStream(png); // kept alive until Save
            using (var gfx = XGraphics.FromPdfPage(page))
            {
                var img = XImage.FromStream(ms);
                gfx.DrawImage(img, 0, 0, page.Width.Point, page.Height.Point);
            }
            doc.Save(path);
            ms.Dispose();
            return path;
        }

        /// <summary>Generate the cheque PDF and open it (Preview) so the user can print at exact size.</summary>
        public static string PreviewOrPrint(ChequeRecord cheque, ChequeProfile profile)
        {
            var path = GeneratePdf(cheque, profile);
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
            return path;
        }

        static Control BuildVisual(ChequeRecord c, ChequeProfile p, double cw, double ch)
        {
            var canvas = new Canvas { Width = cw, Height = ch, Background = Brushes.White, ClipToBounds = true };
            double fs = p.FontSize <= 0 ? 11 : p.FontSize;

            void Add(string? text, double xMm, double yMm, double size, bool bold, double maxWmm = 0)
            {
                if (string.IsNullOrWhiteSpace(text)) return;
                var tb = new TextBlock
                {
                    Text = text, FontSize = size, Foreground = Brushes.Black,
                    FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
                    TextWrapping = TextWrapping.Wrap
                };
                if (maxWmm > 0) tb.MaxWidth = maxWmm * Dip;
                Canvas.SetLeft(tb, (xMm + p.PrintOffsetX) * Dip);
                Canvas.SetTop(tb, (yMm + p.PrintOffsetY) * Dip);
                canvas.Children.Add(tb);
            }

            Add(c.ChequeDate.ToString("dd / MM / yyyy"), p.DateX, p.DateY, fs, p.IsBold);
            Add(c.PayeeName, p.PayeeX, p.PayeeY, fs, true, p.ChequeWidth - p.PayeeX - 5);
            Add($"{c.Currency} {c.Amount:N3}", p.AmountNumX, p.AmountNumY, fs, true);
            Add(c.AmountInWords, p.AmountWordsX, p.AmountWordsY, fs - 1, false, p.ChequeWidth - p.AmountWordsX - 5);
            Add(c.ChequeNumber, p.ChequeNumX, p.ChequeNumY, fs, false);
            return canvas;
        }

        static byte[] RenderPng(Control visual, double cw, double ch, double scale)
        {
            visual.Measure(new Size(cw, ch));
            visual.Arrange(new Rect(0, 0, cw, ch));
            visual.UpdateLayout();

            var pixelSize = new PixelSize(Math.Max(1, (int)(cw * scale)), Math.Max(1, (int)(ch * scale)));
            using var rtb = new RenderTargetBitmap(pixelSize, new Vector(96 * scale, 96 * scale));
            rtb.Render(visual);
            using var ms = new MemoryStream();
            rtb.Save(ms);
            return ms.ToArray();
        }

        static string Safe(string s) => string.Concat((s ?? "").Split(Path.GetInvalidFileNameChars()));
    }
}
