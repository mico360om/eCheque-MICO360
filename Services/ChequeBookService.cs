using Microsoft.Data.Sqlite;
using eCheque.MICO360.Models;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Cheque-book / leaf inventory. Tracks issued leaf ranges per bank account so the app can auto-suggest the
    /// next unused leaf and block duplicate, out-of-range or spoiled cheque numbers. Usage is DERIVED from the
    /// cheque records (never a duplicated counter), so it stays correct across edits, deletes and multi-PC sync.
    /// </summary>
    public static class ChequeBookService
    {
        static int I(SqliteDataReader r, string c) { try { var o = r.GetOrdinal(c); return r.IsDBNull(o) ? 0 : r.GetInt32(o); } catch { return 0; } }
        static string S(SqliteDataReader r, string c) { try { var o = r.GetOrdinal(c); return r.IsDBNull(o) ? "" : r.GetString(o); } catch { return ""; } }

        public static List<ChequeBook> GetBooks(bool activeOnly = false)
        {
            var list = new List<ChequeBook>();
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("SELECT * FROM ChequeBooks" + (activeOnly ? " WHERE Status='Active'" : "") + " ORDER BY BankName, StartNumber", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map(r));
            return list;
        }

        public static ChequeBook? GetBook(int id)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("SELECT * FROM ChequeBooks WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map(r) : null;
        }

        /// <summary>Validates and saves a book. Throws with a clear message on an invalid range or an overlap
        /// with another active book for the same account (which would make leaf numbering ambiguous).</summary>
        public static int SaveBook(ChequeBook b)
        {
            b.BankName = (b.BankName ?? "").Trim();
            b.AccountNumber = (b.AccountNumber ?? "").Trim();
            if (string.IsNullOrWhiteSpace(b.BankName)) throw new InvalidOperationException("Bank is required.");
            if (b.EndNumber < b.StartNumber) throw new InvalidOperationException("End number must be greater than or equal to the start number.");
            if (b.TotalLeaves > 100000) throw new InvalidOperationException("That leaf range is unusually large — please check the start and end numbers.");
            if (OverlapsAnother(b)) throw new InvalidOperationException("This leaf range overlaps another active book for the same account.");

            using var conn = DatabaseService.GetConnection();
            if (b.Id == 0)
            {
                using var cmd = new SqliteCommand("INSERT INTO ChequeBooks(BankName,AccountNumber,BookLabel,Prefix,StartNumber,EndNumber,PadWidth,IssueDate,Status,SpoiledCsv,Notes,CreatedDate,CreatedBy)VALUES(@bn,@ac,@lbl,@px,@sn,@en,@pw,@id,@st,@sp,@nt,@cd,@cb)", conn);
                Fill(cmd, b); cmd.ExecuteNonQuery();
                using var idc = new SqliteCommand("SELECT last_insert_rowid()", conn); b.Id = Convert.ToInt32(idc.ExecuteScalar());
                DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Cheque Book Added", b.DisplayName);
            }
            else
            {
                using var cmd = new SqliteCommand("UPDATE ChequeBooks SET BankName=@bn,AccountNumber=@ac,BookLabel=@lbl,Prefix=@px,StartNumber=@sn,EndNumber=@en,PadWidth=@pw,IssueDate=@id,Status=@st,SpoiledCsv=@sp,Notes=@nt WHERE Id=@rid", conn);
                Fill(cmd, b); cmd.Parameters.AddWithValue("@rid", b.Id); cmd.ExecuteNonQuery();
                DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Cheque Book Updated", b.DisplayName);
            }
            return b.Id;
        }

        public static void SetStatus(int id, string status)
        {
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("UPDATE ChequeBooks SET Status=@s WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@s", status); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Cheque Book " + status, id.ToString());
        }

        public static void MarkSpoiled(int id, int number, bool spoiled = true)
        {
            var b = GetBook(id); if (b == null) return;
            var set = b.SpoiledNumbers();
            if (spoiled) set.Add(number); else set.Remove(number);
            b.SpoiledCsv = ChequeBook.ToCsv(set);
            using var conn = DatabaseService.GetConnection();
            using var cmd = new SqliteCommand("UPDATE ChequeBooks SET SpoiledCsv=@sp WHERE Id=@id", conn);
            cmd.Parameters.AddWithValue("@sp", b.SpoiledCsv); cmd.Parameters.AddWithValue("@id", id); cmd.ExecuteNonQuery();
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", spoiled ? "Cheque Leaf Spoiled" : "Cheque Leaf Restored", b.Format(number));
        }

        // ───────────────────────── usage (derived from cheque records) ─────────────────────────

        /// <summary>The set of leaf numbers in this book's range that are already claimed by a non-cancelled cheque.</summary>
        public static HashSet<int> UsedNumbers(ChequeBook b)
        {
            var used = new HashSet<int>();
            using var conn = DatabaseService.GetConnection();
            var sql = "SELECT ChequeNumber, AccountNumber FROM ChequeRecords WHERE BankName=@bn AND Status<>'Cancelled'";
            using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@bn", b.BankName);
            // Only scope by account when the book specifies one (older cheques may have a blank account).
            using var r = cmd.ExecuteReader();
            bool scopeAcct = !string.IsNullOrWhiteSpace(b.AccountNumber);
            int acctOrd = -1; try { acctOrd = r.GetOrdinal("AccountNumber"); } catch { }
            while (r.Read())
            {
                if (scopeAcct && acctOrd >= 0)
                {
                    var acct = r.IsDBNull(acctOrd) ? "" : r.GetString(acctOrd);
                    if (!string.Equals(acct.Trim(), b.AccountNumber, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(acct)) continue;
                }
                var n = ParseLeaf(r.IsDBNull(0) ? "" : r.GetString(0));
                if (n.HasValue && n.Value >= b.StartNumber && n.Value <= b.EndNumber) used.Add(n.Value);
            }
            return used;
        }

        public static ChequeBookStats Stats(ChequeBook b)
        {
            var used = UsedNumbers(b);
            var spoiled = b.SpoiledNumbers();
            spoiled.RemoveWhere(n => n < b.StartNumber || n > b.EndNumber);

            int? next = null;
            for (int n = b.StartNumber; n <= b.EndNumber; n++)
                if (!used.Contains(n) && !spoiled.Contains(n)) { next = n; break; }

            // Gaps = unused, non-spoiled leaves that sit BELOW the highest used leaf (i.e. skipped out of order).
            var gaps = new List<int>();
            int highestUsed = used.Count > 0 ? used.Max() : b.StartNumber - 1;
            for (int n = b.StartNumber; n < highestUsed; n++)
                if (!used.Contains(n) && !spoiled.Contains(n)) gaps.Add(n);

            int remaining = Math.Max(0, b.TotalLeaves - used.Count - spoiled.Count(n => !used.Contains(n)));
            return new ChequeBookStats(b.TotalLeaves, used.Count, spoiled.Count, remaining, next, gaps, used.OrderBy(x => x).ToList());
        }

        // ───────────────────────── suggestion + validation ─────────────────────────

        /// <summary>The active book that owns <paramref name="number"/> for this bank/account, if any.</summary>
        public static ChequeBook? FindBook(string bank, string account, int number)
        {
            foreach (var b in GetBooks(activeOnly: true))
            {
                if (!string.Equals(b.BankName, bank?.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(b.AccountNumber) && !string.IsNullOrWhiteSpace(account)
                    && !string.Equals(b.AccountNumber, account.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                if (number >= b.StartNumber && number <= b.EndNumber) return b;
            }
            return null;
        }

        /// <summary>Whether any active book is defined for a bank/account (drives whether range checks apply).</summary>
        public static ChequeBook? ActiveBookFor(string bank, string account)
            => GetBooks(activeOnly: true).FirstOrDefault(b =>
                   string.Equals(b.BankName, bank?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrWhiteSpace(b.AccountNumber) || string.IsNullOrWhiteSpace(account) ||
                    string.Equals(b.AccountNumber, account.Trim(), StringComparison.OrdinalIgnoreCase)));

        /// <summary>Next unused, non-spoiled leaf for a bank/account, formatted as it prints. Empty if no active book.</summary>
        public static string NextLeaf(string bank, string account)
        {
            var b = ActiveBookFor(bank, account);
            if (b == null) return "";
            var s = Stats(b);
            return s.NextNumber.HasValue ? b.Format(s.NextNumber.Value) : "";
        }

        public enum LeafCheck { Ok, NoBook, OutOfRange, Spoiled, AlreadyUsed }

        /// <summary>Validates a cheque number against the leaf inventory for a bank/account.
        /// Returns <see cref="LeafCheck.NoBook"/> when no book is defined (the caller may then allow it).</summary>
        public static (LeafCheck result, string message) Validate(string bank, string account, string numberText, int excludeChequeId = 0)
        {
            var parsed = ParseLeaf(numberText);
            var book = ActiveBookFor(bank, account);
            if (book == null) return (LeafCheck.NoBook, "");

            if (!parsed.HasValue || parsed.Value < book.StartNumber || parsed.Value > book.EndNumber)
                return (LeafCheck.OutOfRange, $"Cheque #{numberText} is outside the active book range {book.Format(book.StartNumber)}–{book.Format(book.EndNumber)}.");

            if (book.SpoiledNumbers().Contains(parsed.Value))
                return (LeafCheck.Spoiled, $"Leaf {book.Format(parsed.Value)} is marked spoiled and cannot be used.");

            if (ChequeService.ChequeNumberExists(numberText, bank, excludeChequeId))
                return (LeafCheck.AlreadyUsed, $"Cheque #{numberText} has already been used for {bank}.");

            return (LeafCheck.Ok, "");
        }

        /// <summary>Extracts the trailing run of digits from a cheque number ("A-000123" → 123). Null if none.</summary>
        public static int? ParseLeaf(string? chequeNumber)
        {
            if (string.IsNullOrWhiteSpace(chequeNumber)) return null;
            int end = chequeNumber.Length;
            int i = end;
            while (i > 0 && char.IsDigit(chequeNumber[i - 1])) i--;
            if (i == end) return null;
            // Guard against overflow on absurdly long digit runs.
            var digits = chequeNumber.Substring(i, Math.Min(end - i, 9));
            return int.TryParse(digits, out var n) ? n : null;
        }

        static bool OverlapsAnother(ChequeBook b)
        {
            foreach (var o in GetBooks(activeOnly: true))
            {
                if (o.Id == b.Id) continue;
                if (!string.Equals(o.BankName, b.BankName, StringComparison.OrdinalIgnoreCase)) continue;
                // Only clash when the accounts match (or either is unspecified).
                if (!string.IsNullOrWhiteSpace(o.AccountNumber) && !string.IsNullOrWhiteSpace(b.AccountNumber)
                    && !string.Equals(o.AccountNumber, b.AccountNumber, StringComparison.OrdinalIgnoreCase)) continue;
                if (b.StartNumber <= o.EndNumber && o.StartNumber <= b.EndNumber) return true;
            }
            return false;
        }

        static void Fill(SqliteCommand cmd, ChequeBook b)
        {
            cmd.Parameters.AddWithValue("@bn", b.BankName);
            cmd.Parameters.AddWithValue("@ac", b.AccountNumber);
            cmd.Parameters.AddWithValue("@lbl", b.BookLabel ?? "");
            cmd.Parameters.AddWithValue("@px", b.Prefix ?? "");
            cmd.Parameters.AddWithValue("@sn", b.StartNumber);
            cmd.Parameters.AddWithValue("@en", b.EndNumber);
            cmd.Parameters.AddWithValue("@pw", b.PadWidth <= 0 ? 6 : b.PadWidth);
            cmd.Parameters.AddWithValue("@id", b.IssueDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@st", string.IsNullOrWhiteSpace(b.Status) ? "Active" : b.Status);
            cmd.Parameters.AddWithValue("@sp", b.SpoiledCsv ?? "");
            cmd.Parameters.AddWithValue("@nt", b.Notes ?? "");
            cmd.Parameters.AddWithValue("@cd", b.CreatedDate.ToString("o"));
            cmd.Parameters.AddWithValue("@cb", string.IsNullOrWhiteSpace(b.CreatedBy) ? (AuthService.CurrentUser?.Username ?? "") : b.CreatedBy);
        }

        static ChequeBook Map(SqliteDataReader r) => new()
        {
            Id = I(r, "Id"),
            BankName = S(r, "BankName"),
            AccountNumber = S(r, "AccountNumber"),
            BookLabel = S(r, "BookLabel"),
            Prefix = S(r, "Prefix"),
            StartNumber = I(r, "StartNumber"),
            EndNumber = I(r, "EndNumber"),
            PadWidth = I(r, "PadWidth") <= 0 ? 6 : I(r, "PadWidth"),
            IssueDate = DateTime.TryParse(S(r, "IssueDate"), out var d) ? d : DateTime.Today,
            Status = S(r, "Status"),
            SpoiledCsv = S(r, "SpoiledCsv"),
            Notes = S(r, "Notes"),
            CreatedDate = DateTime.TryParse(S(r, "CreatedDate"), out var cd) ? cd : DateTime.Now,
            CreatedBy = S(r, "CreatedBy")
        };
    }
}
