using PdfSharp.Drawing;
using PdfSharp.Pdf;
using eCheque.MICO360.Models;
using System.IO;
using System.Diagnostics;

namespace eCheque.MICO360.Services
{
    public static class PdfService
    {
        static double Mm(double mm) => mm * 2.8346;

        public static string GeneratePdf(ChequeRecord cheque, ChequeProfile profile)
        {
            var savePath = DatabaseService.GetSetting("PdfSavePath", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "eCheque MICO360", "PDFs"));
            Directory.CreateDirectory(savePath);
            var fullPath = Path.Combine(savePath, $"Cheque_{cheque.ChequeNumber}_{cheque.ChequeDate:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
            using var doc = new PdfDocument();
            doc.Info.Title = $"Cheque {cheque.ChequeNumber}";
            var page = doc.AddPage();
            // The exported PDF is EXACTLY the template's own dimensions — it is never forced to A4/Letter/etc.
            // (it would only be A4 if the template's own size is 210x297). This mirrors the print path, which
            // uses the cheque's dimensions as the page media, so print and export are identical size/orientation.
            double pageWmm = Math.Max(1, profile.ChequeWidth);
            double pageHmm = Math.Max(1, profile.ChequeHeight);
            // Set Width/Height directly and leave Orientation at its Portrait default: PdfSharp SWAPS
            // Width/Height when Orientation is Landscape, so an explicit 190x85 already yields the correct
            // landscape MediaBox without touching Orientation (setting it would re-swap and corrupt the size).
            page.Width  = XUnit.FromMillimeter(pageWmm);
            page.Height = XUnit.FromMillimeter(pageHmm);
            using var gfx = XGraphics.FromPdfPage(page);
            // Only the calibration offset — no arbitrary page margin (that was for placing the cheque on an A4 sheet).
            double ox = Mm(profile.PrintOffsetX);
            double oy = Mm(profile.PrintOffsetY);
            double cw = Mm(profile.ChequeWidth);
            double ch = Mm(profile.ChequeHeight);

            // New visual layout: print ONLY the entered data at the saved positions (no background/decoration).
            if (ChequeRenderer.HasLayout(profile))
            {
                var dateFmt = DatabaseService.GetSetting("DateFormat", "dd/MM/yyyy");
                foreach (var f in ChequeLayout.Parse(profile))
                {
                    if (!f.Enabled) continue;
                    var val = ChequeLayout.ValueFor(f, cheque, dateFmt);
                    if (string.IsNullOrWhiteSpace(val)) continue;
                    var font = new XFont(string.IsNullOrWhiteSpace(f.FontFamily) ? "Arial" : f.FontFamily,
                        f.FontSize <= 0 ? 11 : f.FontSize, f.Bold ? XFontStyleEx.Bold : XFontStyleEx.Regular);
                    var rect = new XRect(ox + Mm(f.X), oy + Mm(f.Y), Mm(Math.Max(10, f.Width)), Mm(14));
                    var fmt = f.Align switch { "Center" => XStringFormats.TopCenter, "Right" => XStringFormats.TopRight, _ => XStringFormats.TopLeft };
                    gfx.DrawString(val, font, XBrushes.Black, rect, fmt);
                }
                // No footer/watermark: the page IS the cheque, so anything extra would print on top of it.
                doc.Save(fullPath);
                return fullPath;
            }

            gfx.DrawRectangle(new XPen(XColor.FromArgb(200,200,200),0.5), XBrushes.White, new XRect(ox,oy,cw,ch));
            gfx.DrawRectangle(XBrushes.LightGray, new XRect(ox,oy,cw,Mm(8)));
            var hFont = new XFont(profile.FontFamily,8,XFontStyleEx.Bold);
            gfx.DrawString($"  {profile.BankName}  |  A/C: {cheque.AccountNumber}", hFont, XBrushes.Black, ox+4, oy+Mm(6));
            void Draw(string text,double fx,double fy,double fs=-1,bool bold=false){var style=(profile.IsBold||bold)?XFontStyleEx.Bold:XFontStyleEx.Regular;var font=new XFont(profile.FontFamily,fs>0?fs:profile.FontSize,style);gfx.DrawString(text,font,XBrushes.Black,ox+Mm(fx),oy+Mm(fy));}
            var lf=new XFont(profile.FontFamily,7,XFontStyleEx.Regular);
            void Lbl(string t,double fx,double fy)=>gfx.DrawString(t,lf,XBrushes.DarkGray,ox+Mm(fx),oy+Mm(fy));
            Lbl("Date:",profile.DateX-12,profile.DateY-3); Draw(cheque.ChequeDate.ToString("dd / MM / yyyy"),profile.DateX,profile.DateY);
            Lbl("Pay to:",profile.PayeeX,profile.PayeeY-3); Draw(cheque.PayeeName,profile.PayeeX,profile.PayeeY,bold:true);
            Lbl("Amount (figures):",profile.AmountNumX-2,profile.AmountNumY-3); Draw($"{cheque.Currency} {cheque.Amount:N3}",profile.AmountNumX,profile.AmountNumY,bold:true);
            Lbl("Amount (words):",profile.AmountWordsX,profile.AmountWordsY-3);
            var wRect=new XRect(ox+Mm(profile.AmountWordsX),oy+Mm(profile.AmountWordsY-1),Mm(profile.ChequeWidth-profile.AmountWordsX-5),Mm(10));
            gfx.DrawString(cheque.AmountInWords,new XFont(profile.FontFamily,profile.FontSize-1,profile.IsBold?XFontStyleEx.Bold:XFontStyleEx.Regular),XBrushes.Black,wRect,XStringFormats.TopLeft);
            Lbl("Cheque No:",profile.ChequeNumX,profile.ChequeNumY-3); Draw(cheque.ChequeNumber,profile.ChequeNumX,profile.ChequeNumY);
            gfx.DrawLine(XPens.Black,ox+cw-Mm(50),oy+ch-Mm(8),ox+cw-Mm(5),oy+ch-Mm(8));
            gfx.DrawString("Authorized Signature",lf,XBrushes.DarkGray,ox+cw-Mm(48),oy+ch-Mm(4));
            // Metadata (Bank/Prepared/Ref/Remarks) and the watermark used to sit BELOW the cheque on an A4 sheet.
            // The page is now exactly the cheque size, so that content no longer has anywhere to go — it is
            // intentionally omitted rather than clipped or bleeding onto the cheque face.
            doc.Save(fullPath);
            return fullPath;
        }

        public static void Open(string path){if(File.Exists(path))Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}

        public static System.Windows.Media.Imaging.BitmapSource? RenderPreview(string pdfPath)
        {
            // No WinRT dependency — return null, preview window falls back to text info
            return null;
        }

        public static void PrintPdf(string pdfPath)
        {
            if (!File.Exists(pdfPath)) throw new FileNotFoundException("PDF not found", pdfPath);
            Process.Start(new ProcessStartInfo(pdfPath) { UseShellExecute = true, Verb = "print" });
        }
    }
}
