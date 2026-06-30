namespace eCheque.MICO360.Models
{
    public class DashboardStats
    {
        public int Total { get; set; }
        public int Printed { get; set; }
        public int Draft { get; set; }
        public int Cancelled { get; set; }
        public int Voided { get; set; }
        public int TodayPrinted { get; set; }
        public int MonthPrinted { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal MonthAmount { get; set; }
        public decimal YearAmount { get; set; }
    }
}
