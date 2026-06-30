namespace eCheque.MICO360.Models
{
    public class PrintHistory
    {
        public int Id { get; set; }
        public int ChequeId { get; set; }
        public string ChequeNumber { get; set; } = "";
        public string PrintedBy { get; set; } = "";
        public DateTime PrintedDate { get; set; } = DateTime.Now;
        public string Reason { get; set; } = "";
        public bool IsReprint { get; set; }
        // joined from ChequeRecords for display
        public string PayeeName { get; set; } = "";
        public string BankName { get; set; } = "";
        public decimal Amount { get; set; }
        public string ReprintLabel => IsReprint ? "Reprint" : "First Print";
    }
}
