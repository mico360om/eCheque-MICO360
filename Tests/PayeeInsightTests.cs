using System;
using System.IO;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>
    /// The entry screen's inline payee history and duplicate-payment advisory: summary reflects only
    /// live (non-cancelled/void) cheques, and the duplicate check matches same payee + same amount
    /// within the window while excluding the cheque being edited.
    /// </summary>
    [Collection("db-serial")]
    public class PayeeInsightTests : IDisposable
    {
        readonly string _dir;

        public PayeeInsightTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "echeque_payee_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            DatabaseService.Initialize(Path.Combine(_dir, "company_payee.db"));
        }

        int Save(string payee, decimal amount, string status = "Printed", string num = "")
            => ChequeService.SaveCheque(new ChequeRecord
            {
                ChequeNumber = string.IsNullOrEmpty(num) ? "N" + Guid.NewGuid().ToString("N")[..8] : num,
                ChequeDate = DateTime.Today, PayeeName = payee, Amount = amount,
                BankName = "Bank Muscat", Status = status, CreatedDate = DateTime.Now
            });

        [Fact]
        public void Summary_reports_count_and_most_recent_cheque()
        {
            Save("Alpha LLC", 100m, num: "000001");
            Save("Alpha LLC", 250.500m, num: "000002");
            Save("Alpha LLC", 999m, status: "Cancelled", num: "000003"); // excluded

            var s = ChequeService.GetPayeeSummary("Alpha LLC");
            Assert.NotNull(s);
            Assert.Equal(2, s!.Value.Count);
            Assert.Equal("000002", s.Value.LastNumber);
            Assert.Equal(250.500m, s.Value.LastAmount);
        }

        [Fact]
        public void Summary_is_null_for_unknown_or_blank_payee()
        {
            Assert.Null(ChequeService.GetPayeeSummary("Nobody Ever Paid"));
            Assert.Null(ChequeService.GetPayeeSummary(""));
        }

        [Fact]
        public void Duplicate_found_for_same_payee_and_amount_within_window()
        {
            Save("Beta Co", 500m, num: "000010");
            Assert.Equal("000010", ChequeService.FindRecentDuplicate("Beta Co", 500m));
            Assert.Null(ChequeService.FindRecentDuplicate("Beta Co", 501m));      // different amount
            Assert.Null(ChequeService.FindRecentDuplicate("Other Co", 500m));     // different payee
        }

        [Fact]
        public void Duplicate_ignores_cancelled_and_the_cheque_being_edited()
        {
            var id = Save("Gamma Co", 75m, num: "000020");
            Assert.Null(ChequeService.FindRecentDuplicate("Gamma Co", 75m, excludeId: id)); // editing itself

            Save("Delta Co", 60m, status: "Cancelled", num: "000021");
            Assert.Null(ChequeService.FindRecentDuplicate("Delta Co", 60m)); // cancelled doesn't count
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
