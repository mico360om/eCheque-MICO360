using System;
using eCheque.MICO360.Models;
using Xunit;

namespace eCheque.MICO360.Tests
{
    public class ChequeRecordTests
    {
        static ChequeRecord Open(int daysFromToday) =>
            new() { Status = "Printed", ChequeDate = DateTime.Today.AddDays(daysFromToday) };

        [Fact] public void DueLabel_today()   => Assert.Equal("Due today",   Open(0).DueLabel);
        [Fact] public void DueLabel_future()  => Assert.Equal("In 5d",       Open(5).DueLabel);
        [Fact] public void DueLabel_overdue() => Assert.Equal("Overdue 3d",  Open(-3).DueLabel);

        [Fact]
        public void DueLabel_empty_when_not_open()
            => Assert.Equal("", new ChequeRecord { Status = "Draft", ChequeDate = DateTime.Today.AddDays(5) }.DueLabel);

        [Fact]
        public void IsPdc_is_future_dated_open_cheques_only()
        {
            Assert.True(Open(5).IsPdc);
            Assert.False(Open(0).IsPdc);
            Assert.False(Open(-1).IsPdc);
            Assert.False(new ChequeRecord { Status = "Cleared", ChequeDate = DateTime.Today.AddDays(5) }.IsPdc);
        }
    }
}
