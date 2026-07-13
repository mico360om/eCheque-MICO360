using System;
using System.IO;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using PdfSharp.Pdf.IO;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>
    /// The exported PDF must be EXACTLY the print template's own dimensions — never silently converted to
    /// A4. These tests generate a real PDF and read the page size back to prove it tracks the profile's
    /// ChequeWidth/ChequeHeight (and matches the print path, which uses the same dimensions as its media).
    /// </summary>
    [Collection("db-serial")]
    public class PdfPageSizeTests : IDisposable
    {
        readonly string _dir;

        public PdfPageSizeTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "echeque_pdfsize_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            DatabaseService.Initialize(Path.Combine(_dir, "company_pdfsize.db"));
            DatabaseService.SaveSetting("PdfSavePath", _dir);
        }

        [Theory]
        [InlineData(190, 85)]    // typical landscape cheque — must NOT become A4 portrait
        [InlineData(203, 90)]    // another custom cheque size
        [InlineData(210, 297)]   // a template whose size genuinely IS A4 — still driven by dimensions
        public void Exported_pdf_matches_the_template_dimensions(double w, double h)
        {
            // PaperSize is deliberately left at "A4" to prove the page size is driven by the template's
            // real dimensions, not by that label.
            var profile = new ChequeProfile { Name = "T", BankName = "Bank", ChequeWidth = w, ChequeHeight = h, PaperSize = "A4" };
            var cheque = new ChequeRecord
            {
                ChequeNumber = "000123", PayeeName = "John Smith", Amount = 100.500m,
                AmountInWords = "one hundred", ChequeDate = DateTime.Today, Currency = "OMR"
            };

            var path = PdfService.GeneratePdf(cheque, profile);
            try
            {
                using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);
                var page = doc.Pages[0];
                Assert.True(Math.Abs(page.Width.Millimeter - w) < 0.5,
                    $"page width {page.Width.Millimeter:0.0}mm != template {w}mm");
                Assert.True(Math.Abs(page.Height.Millimeter - h) < 0.5,
                    $"page height {page.Height.Millimeter:0.0}mm != template {h}mm");
            }
            finally { try { File.Delete(path); } catch { } }
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
