namespace eCheque.MICO360.Models
{
    /// <summary>
    /// A physical cheque book (or leaf range) issued for a bank account. Leaf numbers run from
    /// <see cref="StartNumber"/> to <see cref="EndNumber"/>; the app uses this to auto-suggest the next
    /// unused leaf and to block duplicate / out-of-range / spoiled cheque numbers. Fully synced across PCs.
    /// </summary>
    public class ChequeBook
    {
        public int Id { get; set; }
        public string BankName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public string BookLabel { get; set; } = "";     // free label, e.g. "Book A - 2026"
        public string Prefix { get; set; } = "";         // optional non-numeric prefix printed before the number
        public int StartNumber { get; set; }
        public int EndNumber { get; set; }
        public int PadWidth { get; set; } = 6;           // zero-pad width when formatting a leaf number
        public DateTime IssueDate { get; set; } = DateTime.Today;
        public string Status { get; set; } = "Active";   // Active | Exhausted | Cancelled
        public string SpoiledCsv { get; set; } = "";     // comma-separated spoiled/damaged leaf numbers
        public string Notes { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = "";

        public int TotalLeaves => EndNumber >= StartNumber ? EndNumber - StartNumber + 1 : 0;
        public bool IsActive => Status == "Active";

        /// <summary>Formats a leaf number the way it appears on the cheque (prefix + zero-padded number).</summary>
        public string Format(int n) => Prefix + n.ToString("D" + System.Math.Max(1, PadWidth));

        public HashSet<int> SpoiledNumbers() => ParseCsv(SpoiledCsv);

        public string DisplayName =>
            $"{(string.IsNullOrWhiteSpace(BookLabel) ? BankName : BookLabel)}  ·  {Format(StartNumber)}–{Format(EndNumber)}";

        public static HashSet<int> ParseCsv(string? csv)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(csv)) return set;
            foreach (var part in csv.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part.Trim(), out var n)) set.Add(n);
            return set;
        }

        public static string ToCsv(IEnumerable<int> nums) => string.Join(",", nums.Distinct().OrderBy(x => x));
    }

    /// <summary>Computed usage of a cheque book at a point in time (never persisted — derived from cheque records).</summary>
    public sealed record ChequeBookStats(
        int Total, int Used, int Spoiled, int Remaining,
        int? NextNumber, List<int> Gaps, List<int> UsedNumbers);
}
