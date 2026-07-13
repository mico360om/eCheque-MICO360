using eCheque.MICO360.Helpers;
using Xunit;

namespace eCheque.MICO360.Tests
{
    /// <summary>
    /// Pins the numeric-only rule for the cheque Amount field: digits, one optional decimal point,
    /// at most 3 decimal places (OMR/Baisa). Letters, symbols and malformed numbers are rejected.
    /// </summary>
    public class AmountInputTests
    {
        [Theory]
        [InlineData("")]            // cleared mid-edit
        [InlineData("0")]
        [InlineData("100")]
        [InlineData("100.")]        // partial decimal while typing
        [InlineData("100.5")]
        [InlineData("100.50")]
        [InlineData("100.500")]     // 3 dp — the OMR maximum
        [InlineData("1,234.500")]   // display grouping tolerated
        [InlineData(".5")]
        public void Accepts_valid_amounts(string s) => Assert.True(AmountInput.IsAcceptable(s));

        [Theory]
        [InlineData("abc")]         // letters
        [InlineData("10a")]         // trailing letter
        [InlineData("10.5x")]
        [InlineData("$100")]        // currency symbol
        [InlineData("10-5")]        // stray symbol
        [InlineData("10 00")]       // space
        [InlineData("10.5.5")]      // two decimal points
        [InlineData("100.5000")]    // 4 decimal places — too precise for OMR
        [InlineData("1e3")]         // scientific notation
        [InlineData("-5")]          // negative not allowed
        public void Rejects_invalid_amounts(string s) => Assert.False(AmountInput.IsAcceptable(s));
    }
}
