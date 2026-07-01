using System.Text.Json;

namespace eCheque.MICO360.Models
{
    /// <summary>A single printable field placed on a cheque layout (position in millimetres).</summary>
    public class ChequeField
    {
        public string Key { get; set; } = "";        // Date, Payee, AmountNum, AmountWords, AccountNumber, ChequeNumber, Signature, or Custom
        public string Label { get; set; } = "";
        public double X { get; set; }                 // mm from left
        public double Y { get; set; }                 // mm from top
        public double Width { get; set; } = 55;       // mm (used for alignment / wrapping)
        public double FontSize { get; set; } = 11;
        public string FontFamily { get; set; } = "Arial";
        public bool Bold { get; set; }
        public string Align { get; set; } = "Left";   // Left | Center | Right
        public bool Enabled { get; set; } = true;
        public bool IsCustom { get; set; }
        public string CustomText { get; set; } = "";  // value printed for a custom field
    }

    /// <summary>Serialization + defaults + data mapping for a cheque's field layout.</summary>
    public static class ChequeLayout
    {
        static readonly JsonSerializerOptions Opts = new() { WriteIndented = false };

        public static List<ChequeField> Parse(ChequeProfile p)
        {
            if (!string.IsNullOrWhiteSpace(p.FieldsJson))
            {
                try { var list = JsonSerializer.Deserialize<List<ChequeField>>(p.FieldsJson); if (list is { Count: > 0 }) return list; }
                catch { }
            }
            return Default(p);
        }

        public static string Serialize(List<ChequeField> fields) => JsonSerializer.Serialize(fields, Opts);

        /// <summary>Builds a starter field set from the profile's legacy X/Y columns.</summary>
        public static List<ChequeField> Default(ChequeProfile p) => new()
        {
            new() { Key="Date",          Label="Date",             X=p.DateX,        Y=p.DateY,        Width=40,  FontFamily=p.FontFamily, FontSize=p.FontSize, Bold=p.IsBold },
            new() { Key="Payee",         Label="Payee Name",       X=p.PayeeX,       Y=p.PayeeY,       Width=90,  FontFamily=p.FontFamily, FontSize=p.FontSize, Bold=p.IsBold },
            new() { Key="AmountNum",     Label="Amount (Numbers)", X=p.AmountNumX,   Y=p.AmountNumY,   Width=45,  FontFamily=p.FontFamily, FontSize=p.FontSize, Bold=true },
            new() { Key="AmountWords",   Label="Amount in Words",  X=p.AmountWordsX, Y=p.AmountWordsY, Width=120, FontFamily=p.FontFamily, FontSize=p.FontSize, Bold=p.IsBold },
            new() { Key="AccountNumber", Label="Account Number",   X=25,             Y=Math.Max(5,p.ChequeHeight-20), Width=55, FontFamily=p.FontFamily, FontSize=p.FontSize, Enabled=false },
            new() { Key="ChequeNumber",  Label="Cheque Number",    X=p.ChequeNumX,   Y=p.ChequeNumY,   Width=45,  FontFamily=p.FontFamily, FontSize=p.FontSize, Enabled=false },
            new() { Key="Signature",     Label="Signature",        X=Math.Max(5,p.ChequeWidth-55), Y=Math.Max(5,p.ChequeHeight-18), Width=48, FontFamily=p.FontFamily, FontSize=p.FontSize, Enabled=false },
        };

        /// <summary>The actual value to print for a field, given a cheque.</summary>
        public static string ValueFor(ChequeField f, ChequeRecord c, string dateFormat = "dd/MM/yyyy") => f.Key switch
        {
            "Date"          => c.ChequeDate.ToString(dateFormat),
            "Payee"         => c.PayeeName,
            "AmountNum"     => $"{c.Amount:N3}",
            "AmountWords"   => c.AmountInWords,
            "AccountNumber" => c.AccountNumber,
            "ChequeNumber"  => c.ChequeNumber,
            "Signature"     => "",   // signature is physical; never printed
            _               => f.CustomText,
        };

        /// <summary>Sample data used by the designer's Test Print / preview.</summary>
        public static ChequeRecord SampleCheque(ChequeProfile p) => new()
        {
            ChequeNumber = "000123",
            ChequeDate = DateTime.Today,
            PayeeName = "John Smith Trading LLC",
            Amount = 4956.250m,
            AmountInWords = "FOUR THOUSAND NINE HUNDRED FIFTY-SIX OMANI RIALS AND TWO HUNDRED FIFTY BAISA ONLY",
            AccountNumber = p.AccountNumber,
            BankName = p.BankName,
            Currency = "OMR",
        };
    }
}
