using eCheque.MICO360.Services;
using Xunit;

namespace eCheque.MICO360.Tests
{
    public class AmountToWordsTests
    {
        // Uses the explicit-settings overload so the tests don't depend on the database.
        static string Convert(decimal amount, string caseFormat = "UPPERCASE",
            string currency = "Omani Rials", string baisa = "Baisa", bool includeBaisa = true, bool addOnly = true)
            => AmountToWordsService.Convert(amount, caseFormat, currency, baisa, includeBaisa, addOnly);

        [Fact]
        public void Converts_the_canonical_OMR_example()
        {
            Assert.Equal(
                "FOUR THOUSAND NINE HUNDRED FIFTY-SIX OMANI RIALS AND TWO HUNDRED FIFTY BAISA ONLY",
                Convert(4956.250m));
        }

        [Theory]
        [InlineData(0, "")]
        [InlineData(-5, "")]
        public void Non_positive_amounts_return_empty(double amount, string expected)
            => Assert.Equal(expected, Convert((decimal)amount));

        [Theory]
        [InlineData(1, "ONE OMANI RIALS ONLY")]
        [InlineData(15, "FIFTEEN OMANI RIALS ONLY")]
        [InlineData(70, "SEVENTY OMANI RIALS ONLY")]
        [InlineData(100, "ONE HUNDRED OMANI RIALS ONLY")]
        [InlineData(1000, "ONE THOUSAND OMANI RIALS ONLY")]
        [InlineData(1000000, "ONE MILLION OMANI RIALS ONLY")]
        public void Whole_amounts(double amount, string expected)
            => Assert.Equal(expected, Convert((decimal)amount));

        [Fact]
        public void Baisa_uses_three_decimal_scaling()
            => Assert.Equal("ONE HUNDRED OMANI RIALS AND FIVE HUNDRED BAISA ONLY", Convert(100.500m));

        [Fact]
        public void Hyphenates_compound_tens()
            => Assert.Equal("NINETY-NINE OMANI RIALS ONLY", Convert(99m));

        [Fact]
        public void Include_baisa_false_drops_the_fraction()
            => Assert.Equal("FIVE OMANI RIALS ONLY", Convert(5.250m, includeBaisa: false));

        [Fact]
        public void Add_only_false_drops_the_suffix()
            => Assert.Equal("FIVE OMANI RIALS", Convert(5m, addOnly: false));

        [Theory]
        [InlineData("TitleCase", "Five Omani Rials Only")]
        [InlineData("lowercase", "five omani rials only")]
        [InlineData("UPPERCASE", "FIVE OMANI RIALS ONLY")]
        public void Respects_case_format(string format, string expected)
            => Assert.Equal(expected, Convert(5m, caseFormat: format));

        [Fact]
        public void Honours_custom_currency_wording()
            => Assert.Equal("FIVE US DOLLARS AND FIFTY CENTS ONLY",
                Convert(5.050m, currency: "US Dollars", baisa: "Cents"));
    }
}
