using System.Diagnostics;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Line = Avalonia.Controls.Shapes.Line;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
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

        /// <summary>Build a calibration PDF (ruler + labelled crosshairs at each field) and open it so the user
        /// can print it, lay it over a real cheque, and read off the correct Print Offset. Returns the PDF path.</summary>
        public static string PreviewCalibration(ChequeProfile profile)
        {
            double cw = Math.Max(1, profile.ChequeWidth)  * Dip;
            double ch = Math.Max(1, profile.ChequeHeight) * Dip;
            var visual = BuildCalibrationVisual(profile, cw, ch);
            byte[] png = RenderPng(visual, cw, ch, 3.0);

            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                                   "eCheque MICO360", "PDFs");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"Calibration_{Safe(profile.Name)}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            using var doc = new PdfDocument();
            doc.Info.Title = $"Calibration — {profile.Name}";
            var page = doc.AddPage();
            page.Width  = XUnit.FromMillimeter(profile.ChequeWidth);
            page.Height = XUnit.FromMillimeter(profile.ChequeHeight);
            var ms = new MemoryStream(png);
            using (var gfx = XGraphics.FromPdfPage(page))
            {
                var img = XImage.FromStream(ms);
                gfx.DrawImage(img, 0, 0, page.Width.Point, page.Height.Point);
            }
            doc.Save(path);
            ms.Dispose();
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
            return path;
        }

        static Control BuildCalibrationVisual(ChequeProfile p, double cw, double ch)
        {
            double wmm = Math.Max(1, p.ChequeWidth), hmm = Math.Max(1, p.ChequeHeight);
            var canvas = new Canvas { Width = cw, Height = ch, Background = Brushes.White, ClipToBounds = true };

            canvas.Children.Add(new Rectangle { Width = cw, Height = ch, Stroke = Brushes.Black, StrokeThickness = 0.6 });

            for (int mm = 0; mm <= wmm; mm += 5)
            {
                bool major = mm % 10 == 0;
                double x = mm * Dip, len = (major ? 4.0 : 2.0) * Dip;
                canvas.Children.Add(Seg(x, 0, x, len));
                if (major && mm > 0) canvas.Children.Add(Text($"{mm}", x + 0.4 * Dip, 0.2 * Dip, 6.5));
            }
            for (int mm = 0; mm <= hmm; mm += 5)
            {
                bool major = mm % 10 == 0;
                double y = mm * Dip, len = (major ? 4.0 : 2.0) * Dip;
                canvas.Children.Add(Seg(0, y, len, y));
                if (major && mm > 0) canvas.Children.Add(Text($"{mm}", 0.3 * Dip, y + 0.2 * Dip, 6.5));
            }

            foreach (var f in ChequeLayout.Parse(p))
            {
                if (!f.Enabled) continue;
                double x = (f.X + p.PrintOffsetX) * Dip, y = (f.Y + p.PrintOffsetY) * Dip, arm = 3.0 * Dip;
                canvas.Children.Add(Seg(x - arm, y, x + arm, y));
                canvas.Children.Add(Seg(x, y - arm, x, y + arm));
                canvas.Children.Add(Text($"{FieldName(f.Key)}  ({f.X:0}, {f.Y:0})", x + 1.2 * Dip, y + 0.6 * Dip, 7));
            }

            string off = (p.PrintOffsetX == 0 && p.PrintOffsetY == 0) ? "no offset" : $"offset X={p.PrintOffsetX:0.#}, Y={p.PrintOffsetY:0.#} mm";
            canvas.Children.Add(Text($"CALIBRATION — {p.Name}  •  {wmm:0}×{hmm:0} mm  •  {off}  •  ruler in mm from top-left",
                2 * Dip, ch - 5.5 * Dip, 7));
            return canvas;
        }

        static Line Seg(double x1, double y1, double x2, double y2) => new()
        { StartPoint = new Point(x1, y1), EndPoint = new Point(x2, y2), Stroke = Brushes.Black, StrokeThickness = 0.5 };

        static TextBlock Text(string t, double x, double y, double size)
        {
            var tb = new TextBlock { Text = t, FontSize = size, Foreground = Brushes.Black };
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
            return tb;
        }

        static string FieldName(string key) => key switch
        {
            "Date" => "Date",
            "Payee" => "Payee",
            "AmountNum" => "Amount (fig)",
            "AmountWords" => "Amount (words)",
            "ChequeNumber" => "Cheque #",
            _ => key
        };

        static Control BuildVisual(ChequeRecord c, ChequeProfile p, double cw, double ch)
        {
            var canvas = new Canvas { Width = cw, Height = ch, Background = Brushes.White, ClipToBounds = true };

            // Render the saved visual layout (falls back to the profile's default field set for old profiles),
            // so what the Designer positions is exactly what prints. The scanned template is never printed.
            foreach (var f in ChequeLayout.Parse(p))
            {
                if (!f.Enabled) continue;
                var val = ChequeLayout.ValueFor(f, c);
                if (string.IsNullOrWhiteSpace(val)) continue;

                var tb = new TextBlock
                {
                    Text = val,
                    FontSize = f.FontSize <= 0 ? 11 : f.FontSize,
                    Foreground = Brushes.Black,
                    FontWeight = f.Bold ? FontWeight.Bold : FontWeight.Normal,
                    TextWrapping = TextWrapping.Wrap,
                    Width = Math.Max(10, f.Width) * Dip,
                    TextAlignment = f.Align switch { "Center" => TextAlignment.Center, "Right" => TextAlignment.Right, _ => TextAlignment.Left }
                };
                Canvas.SetLeft(tb, (f.X + p.PrintOffsetX) * Dip);
                Canvas.SetTop(tb, (f.Y + p.PrintOffsetY) * Dip);
                canvas.Children.Add(tb);
            }
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
