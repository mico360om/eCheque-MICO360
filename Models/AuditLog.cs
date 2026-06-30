namespace eCheque.MICO360.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserName { get; set; } = "";
        public string Action { get; set; } = "";
        public string RecordReference { get; set; } = "";
        public string Remarks { get; set; } = "";
        public DateTime ActionDate { get; set; } = DateTime.Now;
    }
}
