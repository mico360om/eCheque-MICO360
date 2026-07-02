namespace eCheque.MICO360.Models
{
    public class ChequeProfile
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string BankName { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string AccountNumber { get; set; } = "";
        public double ChequeWidth { get; set; } = 190;
        public double ChequeHeight { get; set; } = 85;
        public double DateX { get; set; } = 140;
        public double DateY { get; set; } = 18;
        public double PayeeX { get; set; } = 25;
        public double PayeeY { get; set; } = 35;
        public double AmountNumX { get; set; } = 140;
        public double AmountNumY { get; set; } = 35;
        public double AmountWordsX { get; set; } = 25;
        public double AmountWordsY { get; set; } = 50;
        public double ChequeNumX { get; set; } = 25;
        public double ChequeNumY { get; set; } = 65;
        public string FontFamily { get; set; } = "Arial";
        public double FontSize { get; set; } = 11;
        public bool IsBold { get; set; }
        public double PrintOffsetX { get; set; }
        public double PrintOffsetY { get; set; }
        public string PaperSize { get; set; } = "A4";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public string CreatedBy { get; set; } = "";
        public int LastChequeNumber { get; set; }
        // Visual designer: base64 PNG of the scanned cheque used as an on-screen template (never printed),
        // and the saved field layout (positions/fonts/alignment) as JSON.
        public string BackgroundImage { get; set; } = "";
        public string FieldsJson { get; set; } = "";

        /// <summary>True when this profile has a scanned template and/or a saved visual field layout.</summary>
        public bool HasTemplate => !string.IsNullOrWhiteSpace(BackgroundImage) || !string.IsNullOrWhiteSpace(FieldsJson);
        public string PaperDisplay => $"{ChequeWidth:0}×{ChequeHeight:0} mm · {PaperSize}";
    }
}
