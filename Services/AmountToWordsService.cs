namespace eCheque.MICO360.Services
{
    public static class AmountToWordsService
    {
        private static readonly string[] Ones = { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
        private static readonly string[] Tens = { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

        /// <summary>
        /// Convert amount to words using app settings for formatting.
        /// Reads: AmountCaseFormat, AmountCurrencyWording, AmountIncludeBaisa, AmountAddOnly
        /// </summary>
        public static string Convert(decimal amount)
        {
            var caseFormat   = DatabaseService.GetSetting("AmountCaseFormat",    "UPPERCASE");
            var currencyWord = DatabaseService.GetSetting("AmountCurrencyWording","Omani Rials");
            var baisaWord    = DatabaseService.GetSetting("AmountBaisaWording",  "Baisa");
            var includeBaisa = DatabaseService.GetSetting("AmountIncludeBaisa",  "true") == "true";
            var addOnly      = DatabaseService.GetSetting("AmountAddOnly",       "true") == "true";
            return Convert(amount, caseFormat, currencyWord, baisaWord, includeBaisa, addOnly);
        }

        /// <summary>Overload accepting explicit settings — used by preview/test in Settings.</summary>
        public static string Convert(decimal amount, string caseFormat, string currencyWord = "Omani Rials",
            string baisaWord = "Baisa", bool includeBaisa = true, bool addOnly = true)
        {
            if (amount <= 0) return "";
            long rials = (long)Math.Floor(amount);
            // OMR has 3 decimal places (Baisa = 1/1000)
            long baisa = (long)Math.Round((amount - rials) * 1000);

            string result;
            if (includeBaisa && baisa > 0)
                result = $"{N2W(rials)} {currencyWord} and {N2W(baisa)} {baisaWord}";
            else
                result = $"{N2W(rials)} {currencyWord}";

            if (addOnly) result += " Only";

            return caseFormat switch
            {
                "UPPERCASE"  => result.ToUpper(),
                "lowercase"  => result.ToLower(),
                "TitleCase"  => ToTitleCase(result),
                _            => result.ToUpper()
            };
        }

        private static string N2W(long n)
        {
            if (n == 0) return "Zero";
            var w = "";
            if (n / 1_000_000 > 0) { w += N2W(n / 1_000_000) + " Million "; n %= 1_000_000; }
            if (n / 1000 > 0)      { w += N2W(n / 1000) + " Thousand "; n %= 1000; }
            if (n / 100 > 0)       { w += Ones[n / 100] + " Hundred "; n %= 100; }
            if (n > 0)
            {
                if (n < 20) w += Ones[n];
                else        { w += Tens[n / 10]; if (n % 10 > 0) w += "-" + Ones[n % 10]; }
            }
            return w.Trim();
        }

        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var words = input.ToLower().Split(' ');
            return string.Join(" ", words.Select(w =>
                w.Length == 0 ? w : char.ToUpper(w[0]) + w[1..]));
        }
    }
}
