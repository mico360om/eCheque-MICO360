using System;
using System.IO;
using System.Linq;
using eCheque.MICO360.Services;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>Batch import parses payee/amount lists from CSV (and Excel) with flexible column order,
    /// tolerating headers, grouping commas, quoted fields and a currency prefix.</summary>
    public class BatchImportTests : IDisposable
    {
        readonly string _dir;
        public BatchImportTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "echeque_batch_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        string WriteCsv(string content)
        {
            var p = Path.Combine(_dir, "batch.csv");
            File.WriteAllText(p, content);
            return p;
        }

        [Fact]
        public void Parses_headered_csv_with_flexible_columns()
        {
            var path = WriteCsv(
                "Payee,Amount,Date,Reference\n" +
                "Alpha Trading,\"1,250.500\",02/07/2026,INV-1\n" +
                "Beta LLC,OMR 90,2026-07-05,INV-2\n");
            var rows = BatchImportService.Parse(path);

            Assert.Equal(2, rows.Count);
            Assert.Equal("Alpha Trading", rows[0].PayeeName);
            Assert.Equal(1250.500m, rows[0].Amount);
            Assert.Equal(new DateTime(2026, 7, 2), rows[0].ChequeDate);
            Assert.Equal("INV-1", rows[0].Reference);
            Assert.Equal(90m, rows[1].Amount);              // "OMR 90" -> 90
        }

        [Fact]
        public void Falls_back_to_first_two_columns_when_no_header()
        {
            var path = WriteCsv("Gamma Co,45.750\nDelta Co,12\n");
            var rows = BatchImportService.Parse(path);
            Assert.Equal(2, rows.Count);
            Assert.Equal("Gamma Co", rows[0].PayeeName);
            Assert.Equal(45.750m, rows[0].Amount);
            Assert.Equal(12m, rows[1].Amount);
        }

        [Fact]
        public void Skips_blank_lines_and_numbers_rows()
        {
            var path = WriteCsv("Payee,Amount\nA,10\n\nB,20\n");
            var rows = BatchImportService.Parse(path);
            Assert.Equal(2, rows.Count);
            Assert.Equal(1, rows[0].RowNo);
            Assert.Equal(2, rows[1].RowNo);
        }

        public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }
    }
}
