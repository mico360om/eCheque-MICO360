using eCheque.MICO360.Services;
using Xunit;

namespace eCheque.MICO360.Tests
{
    public class UpdateServiceTests
    {
        [Theory]
        [InlineData("1.0.1", "1.0.0", true)]
        [InlineData("1.1.0", "1.0.9", true)]
        [InlineData("2.0.0", "1.9.9", true)]
        [InlineData("v1.2.0", "1.1.0", true)]   // tolerates leading 'v'
        [InlineData("1.0", "1.0.0", false)]     // 1.0 normalizes to 1.0.0
        [InlineData("1.0.0", "1.0.0", false)]
        [InlineData("1.0.0", "1.0.1", false)]   // older is not newer
        public void IsNewer_compares_versions(string latest, string current, bool expected)
            => Assert.Equal(expected, UpdateService.IsNewer(latest, current));

        [Fact]
        public void IsNewer_handles_short_tags()
            => Assert.True(UpdateService.IsNewer("2", "1.9.9"));
    }

    public class ChequeStatusGuardTests
    {
        [Theory]
        [InlineData("Printed", true)]
        [InlineData("Reprinted", true)]
        [InlineData("Presented", true)]
        [InlineData("Cleared", true)]
        [InlineData("Bounced", true)]
        [InlineData("Cancelled", true)]
        [InlineData("Void", true)]
        [InlineData("Draft", false)]
        [InlineData("ReadyToPrint", false)]
        public void IsLocked_flags_issued_or_closed_cheques(string status, bool expected)
            => Assert.Equal(expected, ChequeService.IsLocked(status));

        [Theory]
        [InlineData("Cancelled", true)]
        [InlineData("Void", true)]
        [InlineData("Cleared", true)]
        [InlineData("Bounced", true)]
        [InlineData("Printed", false)]
        [InlineData("Reprinted", false)]
        [InlineData("Presented", false)]
        [InlineData("Draft", false)]
        public void IsPrintBlocked_blocks_closed_and_settled(string status, bool expected)
            => Assert.Equal(expected, ChequeService.IsPrintBlocked(status));
    }
}
