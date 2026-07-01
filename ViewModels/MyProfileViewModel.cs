using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    /// <summary>Self-service profile — available to EVERY signed-in user (Admin, Accountant, Viewer).</summary>
    public class MyProfileViewModel : BaseViewModel
    {
        string _fullName = "", _email = "", _username = "", _role = "", _profileStatus = "", _pwdStatus = "";

        public string FullName { get => _fullName; set => Set(ref _fullName, value); }
        public string Email    { get => _email;    set => Set(ref _email, value); }
        public string Username { get => _username; set => Set(ref _username, value); }
        public string Role     { get => _role;     set => Set(ref _role, value); }
        public string ProfileStatus { get => _profileStatus; set => Set(ref _profileStatus, value); }
        public string PasswordStatus { get => _pwdStatus; set => Set(ref _pwdStatus, value); }

        public ICommand SaveProfileCommand { get; }

        public MyProfileViewModel()
        {
            SaveProfileCommand = new RelayCommand(SaveProfile);
            Load();
        }

        public void Load()
        {
            var u = AuthService.CurrentUser;
            FullName = u?.FullName ?? "";
            Email = u?.Email ?? "";
            Username = u?.Username ?? "";
            Role = u?.Role ?? "";
            ProfileStatus = ""; PasswordStatus = "";
        }

        void SaveProfile()
        {
            var err = AuthService.UpdateOwnProfile(FullName, Email);
            ProfileStatus = err ?? "Profile updated.";
        }

        /// <summary>Called from the view (passwords come from PasswordBoxes, not data-binding).</summary>
        public void ChangePassword(string current, string newPwd, string confirm)
        {
            if (string.IsNullOrWhiteSpace(current)) { PasswordStatus = "Enter your current password."; return; }
            if (newPwd != confirm) { PasswordStatus = "New password and confirmation do not match."; return; }
            var strong = CheckStrength(newPwd);
            if (strong != null) { PasswordStatus = strong; return; }

            var me = AuthService.CurrentUser;
            if (me == null) { PasswordStatus = "No user is signed in."; return; }
            if (!AuthService.ChangePassword(me.Id, current, newPwd)) { PasswordStatus = "Current password is incorrect."; return; }
            PasswordStatus = "Password changed successfully.";
        }

        static string? CheckStrength(string pwd)
        {
            if (pwd.Length < 8) return "Password must be at least 8 characters.";
            if (!pwd.Any(char.IsLetter) || !pwd.Any(char.IsDigit)) return "Password must contain both letters and numbers.";
            return null;
        }
    }
}
