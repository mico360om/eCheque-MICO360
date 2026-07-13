using System.Globalization;
using System.IO;
using System.Text;
using OfficeOpenXml;
using eCheque.MICO360.Models;

namespace eCheque.MICO360.Services
{
    /// <summary>Reads a batch of cheques (payee + amount + optional date/reference/invoice/memo) from an
    /// Excel (.xlsx) or CSV file. Column order is flexible: headers are matched by name; if no recognizable
    /// header row is present, the first two columns are taken as Payee and Amount.</summary>
    public static class BatchImportService
    {
        static BatchImportService() { ExcelPackage.LicenseContext = LicenseContext.NonCommercial; }

        public static List<BatchRow> Parse(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".csv" ? ParseCsv(File.ReadAllLines(path)) : ParseXlsx(path);
        }

        // ---- Excel ----
        static List<BatchRow> ParseXlsx(string path)
        {
            using var pkg = new ExcelPackage(new FileInfo(path));
            var ws = pkg.Workbook.Worksheets.FirstOrDefault()
                     ?? throw new InvalidOperationException("The Excel file has no worksheets.");
            if (ws.Dimension == null) return new();

            int rows = ws.Dimension.End.Row, cols = ws.Dimension.End.Column;
            var grid = new List<string[]>();
            for (int r = 1; r <= rows; r++)
            {
                var line = new string[cols];
                for (int c = 1; c <= cols; c++) line[c - 1] = ws.Cells[r, c].Text?.Trim() ?? "";
                grid.Add(line);
            }
            return FromGrid(grid);
        }

        // ---- CSV ----
        static List<BatchRow> ParseCsv(string[] lines)
        {
            var grid = new List<string[]>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                grid.Add(SplitCsv(line));
            }
            return FromGrid(grid);
        }

        static string[] SplitCsv(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool q = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (q)
                {
                    if (ch == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; } else q = false; }
                    else sb.Append(ch);
                }
                else if (ch == '"') q = true;
                else if (ch == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(ch);
            }
            fields.Add(sb.ToString());
            return fields.Select(f => f.Trim()).ToArray();
        }

        // ---- shared: map a grid of cells to rows ----
        static List<BatchRow> FromGrid(List<string[]> grid)
        {
            var list = new List<BatchRow>();
            if (grid.Count == 0) return list;

            // Header detection: a first row with no numeric amount but a recognizable "payee/amount" label.
            var header = grid[0].Select(h => h.ToLowerInvariant()).ToArray();
            bool hasHeader = header.Any(h => h.Contains("payee") || h.Contains("name"))
                          && header.Any(h => h.Contains("amount") || h.Contains("value"));

            int ciPayee = 0, ciAmount = 1, ciNum = -1, ciDate = -1, ciRef = -1, ciInv = -1, ciMemo = -1;
            int start = 0;
            if (hasHeader)
            {
                start = 1;
                for (int i = 0; i < header.Length; i++)
                {
                    var h = header[i];
                    if (h.Contains("payee") || (h.Contains("name") && ciPayee == 0)) ciPayee = i;
                    else if (h.Contains("amount") || h.Contains("value")) ciAmount = i;
                    else if (h.Contains("cheque") || h.Contains("check") || h.Contains("number") || h == "no" || h == "no.") ciNum = i;
                    else if (h.Contains("date")) ciDate = i;
                    else if (h.Contains("ref")) ciRef = i;
                    else if (h.Contains("invoice")) ciInv = i;
                    else if (h.Contains("memo") || h.Contains("remark") || h.Contains("note")) ciMemo = i;
                }
            }

            int rowNo = 0;
            for (int r = start; r < grid.Count; r++)
            {
                var cells = grid[r];
                string Cell(int idx) => idx >= 0 && idx < cells.Length ? cells[idx] : "";

                var payee = Cell(ciPayee);
                var amountText = Cell(ciAmount);
                if (string.IsNullOrWhiteSpace(payee) && string.IsNullOrWhiteSpace(amountText)) continue; // blank line

                var row = new BatchRow
                {
                    RowNo = ++rowNo,
                    PayeeName = payee,
                    Amount = ParseAmount(amountText),
                    ChequeNumber = Cell(ciNum),
                    ChequeDate = ParseDate(Cell(ciDate)),
                    Reference = Cell(ciRef),
                    Invoice = Cell(ciInv),
                    Memo = Cell(ciMemo)
                };
                list.Add(row);
            }
            return list;
        }

        static decimal ParseAmount(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Replace(",", "").Replace("OMR", "", StringComparison.OrdinalIgnoreCase).Trim();
            return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
        }

        static DateTime ParseDate(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return DateTime.Today;
            foreach (var fmt in new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "dd-MM-yyyy" })
                if (DateTime.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
            return DateTime.TryParse(s, out var g) ? g : DateTime.Today;
        }
    }
}
