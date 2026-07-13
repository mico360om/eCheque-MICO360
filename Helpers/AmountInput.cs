using System.Text.RegularExpressions;

namespace eCheque.MICO360.Helpers
{
    /// <summary>
    /// Pure validation for the cheque Amount field, kept out of the view so it can be unit-tested.
    /// An amount is digits with an optional decimal point and at most 3 decimal places (OMR uses 3 —
    /// 1000 Baisa = 1 Rial). Display grouping commas are ignored because they are formatting applied on
    /// commit, not something the user types. Empty is accepted so the field can be cleared mid-edit.
    /// </summary>
    public static class AmountInput
    {
        static readonly Regex Partial = new(@"^\d*(\.\d{0,3})?$", RegexOptions.Compiled);

        /// <summary>True if <paramref name="candidate"/> is a valid (possibly partial) amount.</summary>
        public static bool IsAcceptable(string? candidate)
            => Partial.IsMatch((candidate ?? "").Replace(",", ""));
    }
}
