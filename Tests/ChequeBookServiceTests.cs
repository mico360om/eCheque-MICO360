using System;
using System.IO;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>
    /// Cheque-book / leaf inventory: usage is derived from real cheque records, and the validator must block
    /// out-of-range, spoiled and duplicate leaves while allowing everything when no book is defined.
    /// </summary>
    [Collection("db-serial")]
    public class ChequeBookServiceTests : IDisposable
    {
        readonly string _dir;
        const string Bank = "Bank Muscat";

        public ChequeBookServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "echeque_book_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            DatabaseService.Initialize(Path.Combine(_dir, "company_book.db"));
        }

        int NewBook(int start, int end) => ChequeBookService.SaveBook(new ChequeBook
        { BankName = Bank, StartNumber = start, EndNumber = end, PadWidth = 6, Status = "Active" });

        void UseCheque(string number, string status = "Printed") => ChequeService.SaveCheque(new ChequeRecord
        { ChequeNumber = number, ChequeDate = DateTime.Today, PayeeName = "T", Amount = 1, BankName = Bank, Status = status, CreatedDate = DateTime.Now });

        [Fact]
        public void Stats_derives_used_remaining_next_and_gaps_from_cheques()
        {
            var id = NewBook(1, 10);
            UseCheque("000001");
            UseCheque("000003");
            ChequeBookService.MarkSpoiled(id, 5);

            var book = ChequeBookService.GetBook(id)!;
            var s = ChequeBookService.Stats(book);

            Assert.Equal(10, s.Total);
            Assert.Equal(2, s.Used);                 // 1 and 3
            Assert.Equal(1, s.Spoiled);              // 5
            Assert.Equal(7, s.Remaining);            // 10 - 2 used - 1 spoiled
            Assert.Equal(2, s.NextNumber);           // smallest unused, non-spoiled
            Assert.Equal(new[] { 2 }, s.Gaps);       // below the highest used (3): 2 is skipped
        }

        [Fact]
        public void NextLeaf_skips_used_and_spoiled_leaves()
        {
            var id = NewBook(100, 110);
            UseCheque("000100");
            ChequeBookService.MarkSpoiled(id, 101);
            Assert.Equal("000102", ChequeBookService.NextLeaf(Bank, ""));
        }

        [Fact]
        public void Validate_blocks_out_of_range_spoiled_and_duplicate_but_allows_in_range()
        {
            var id = NewBook(1, 10);
            UseCheque("000001");
            ChequeBookService.MarkSpoiled(id, 5);

            Assert.Equal(ChequeBookService.LeafCheck.Ok,          ChequeBookService.Validate(Bank, "", "000002").result);
            Assert.Equal(ChequeBookService.LeafCheck.OutOfRange,  ChequeBookService.Validate(Bank, "", "000011").result);
            Assert.Equal(ChequeBookService.LeafCheck.Spoiled,     ChequeBookService.Validate(Bank, "", "000005").result);
            Assert.Equal(ChequeBookService.LeafCheck.AlreadyUsed, ChequeBookService.Validate(Bank, "", "000001").result);
        }

        [Fact]
        public void Usage_is_scoped_to_the_book_account_when_specified()
        {
            // A book bound to account "ACC-1" must NOT count a cheque drawn on a different account.
            var id = ChequeBookService.SaveBook(new ChequeBook
            { BankName = Bank, AccountNumber = "ACC-1", StartNumber = 1, EndNumber = 10, PadWidth = 6, Status = "Active" });
            ChequeService.SaveCheque(new ChequeRecord
            { ChequeNumber = "000002", ChequeDate = DateTime.Today, PayeeName = "T", Amount = 1, BankName = Bank, AccountNumber = "ACC-2", Status = "Printed", CreatedDate = DateTime.Now });

            var book = ChequeBookService.GetBook(id)!;
            var s = ChequeBookService.Stats(book);
            Assert.Equal(0, s.Used);                 // the ACC-2 cheque is out of this book's account scope
            Assert.Equal(1, s.NextNumber);
        }

        [Fact]
        public void Validate_returns_NoBook_when_no_active_book_exists()
            => Assert.Equal(ChequeBookService.LeafCheck.NoBook, ChequeBookService.Validate("Some Other Bank", "", "000123").result);

        [Fact]
        public void SaveBook_rejects_overlapping_active_ranges()
        {
            NewBook(1, 50);
            var ex = Assert.Throws<InvalidOperationException>(() => NewBook(40, 90));
            Assert.Contains("overlap", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SaveBook_rejects_inverted_range()
            => Assert.Throws<InvalidOperationException>(() => NewBook(100, 50));

        [Theory]
        [InlineData("000123", 123)]
        [InlineData("A-000045", 45)]
        [InlineData("CHQ 7", 7)]
        [InlineData("abc", null)]
        [InlineData("", null)]
        public void ParseLeaf_extracts_trailing_digits(string input, int? expected)
            => Assert.Equal(expected, ChequeBookService.ParseLeaf(input));

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
