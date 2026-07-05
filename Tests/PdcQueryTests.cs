using System;
using System.IO;
using System.Linq;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>
    /// The PDC/reconciliation queries were moved from in-memory filtering to SQL. These tests pin the SQL
    /// semantics to the model's own definitions (IsIssued / DaysUntilDue) against a REAL encrypted database,
    /// so a future query change that drifts from the model breaks loudly.
    /// </summary>
    [Collection("db-serial")]
    public class PdcQueryTests : IDisposable
    {
        readonly string _dir;

        public PdcQueryTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "echeque_pdcq_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            DatabaseService.Initialize(Path.Combine(_dir, "company_pdcq.db"));

            Save("Printed",   -3);  // overdue issued            -> counted
            Save("Presented",  0);  // due today, issued         -> counted
            Save("Reprinted",  5);  // due in 5d, issued         -> counted within 7
            Save("Printed",   30);  // far future, issued        -> NOT within 7
            Save("Draft",      1);  // not issued                -> never
            Save("Cleared",    1);  // settled                   -> never
            Save("Bounced",   -1);  // settled                   -> never
        }

        void Save(string status, int daysFromToday) => ChequeService.SaveCheque(new ChequeRecord
        {
            ChequeNumber = $"N{status}{daysFromToday}",
            ChequeDate = DateTime.Today.AddDays(daysFromToday),
            PayeeName = "T", Amount = 1, Status = status, CreatedDate = DateTime.Now
        });

        [Fact]
        public void Sql_queries_match_the_model_definitions()
        {
            // Ground truth computed the old way: full load + model properties.
            var all = ChequeService.GetCheques();
            Assert.Equal(7, all.Count);

            Assert.Equal(all.Count(c => c.IsIssued && c.DaysUntilDue <= 7),  ChequeService.GetDuePdcCount(7));   // 3
            Assert.Equal(all.Count(c => c.IsIssued && c.DaysUntilDue <= 60), ChequeService.GetDuePdcCount(60));  // 4
            Assert.Equal(all.Count(c => c.IsIssued),                         ChequeService.GetPdcCheques().Count);
            Assert.Equal(3, ChequeService.GetDuePdcCheques(7).Count);

            // Soonest-first ordering, overdue at the top.
            var due = ChequeService.GetDuePdcCheques(7);
            Assert.True(due.Zip(due.Skip(1)).All(p => p.First.ChequeDate <= p.Second.ChequeDate));
        }

        [Fact]
        public void Limit_caps_recent_cheques_in_sql()
        {
            Assert.Equal(2, ChequeService.GetCheques(limit: 2).Count);
            Assert.Equal(7, ChequeService.GetCheques().Count); // no limit -> unchanged behaviour
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { Directory.Delete(_dir, true); } catch { }
        }
    }
}
