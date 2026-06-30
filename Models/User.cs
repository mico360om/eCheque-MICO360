namespace eCheque.MICO360.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "Accountant";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime? LastLogin { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutUntil { get; set; }
    }
}
