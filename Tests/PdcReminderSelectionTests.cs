using System;
using System.Collections.Generic;
using System.Linq;
using eCheque.MICO360.Models;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>
    /// Locks down which cheques a PDC reminder includes. ChequeService.GetDuePdcCheques reads from the DB and
    /// then applies exactly this predicate — <c>c.IsIssued &amp;&amp; c.DaysUntilDue &lt;= days</c> — so these tests
    /// characterise the selection semantics (which live in the ChequeRecord model) without needing a database.
    /// </summary>
    public class PdcReminderSelectionTests
    {
        static ChequeRecord Rec(string status, int daysFromToday) =>
            new() { Status = status, ChequeDate = DateTime.Today.AddDays(daysFromToday) };

        // Mirror of the GetDuePdcCheques filter, so a change to IsIssued/DaysUntilDue that breaks reminders fails here.
        static List<ChequeRecord> Due(IEnumerable<ChequeRecord> all, int days) =>
            all.Where(c => c.IsIssued && c.DaysUntilDue <= days).OrderBy(c => c.ChequeDate).ToList();

        [Fact]
        public void Includes_issued_cheques_due_today_and_within_window()
        {
            var all = new[] { Rec("Printed", 0), Rec("Reprinted", 3), Rec("Presented", 7) };
            Assert.Equal(3, Due(all, 7).Count);
        }

        [Fact]
        public void Includes_overdue_issued_cheques()
        {
            var all = new[] { Rec("Printed", -10) };
            Assert.Single(Due(all, 7));
        }

        [Fact]
        public void Excludes_cheques_due_beyond_the_window()
        {
            var all = new[] { Rec("Printed", 8), Rec("Printed", 30) };
            Assert.Empty(Due(all, 7));
        }

        [Fact]
        public void Excludes_non_issued_cheques()
        {
            // Draft / Cancelled / Cleared / Bounced are not "issued" and must never be reminded about.
            var all = new[] { Rec("Draft", 1), Rec("Cancelled", 2), Rec("Cleared", 3), Rec("Bounced", 4) };
            Assert.Empty(Due(all, 7));
        }

        [Fact]
        public void Orders_by_cheque_date_soonest_first_including_overdue()
        {
            var all = new[] { Rec("Printed", 5), Rec("Printed", -2), Rec("Printed", 0) };
            var due = Due(all, 7);
            Assert.Equal(new[] { -2, 0, 5 }, due.Select(c => c.DaysUntilDue).ToArray());
        }
    }
}
