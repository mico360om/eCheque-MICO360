namespace eCheque.MICO360.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string TradeName { get; set; } = "";
        public string Address { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Currency { get; set; } = "OMR";
        public string CreatedDate { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
