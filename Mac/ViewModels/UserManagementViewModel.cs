using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    /// <summary>Admin screen: list all users and create/edit them (mirrors the Windows User Management screen).</summary>
    public class UserManagementViewModel : ViewModelBase
    {
        User? _selected;
        User _edit = new() { Role = "Viewer", IsActive = true };
        string _newPassword = "", _status = "";
        bool _isEditing;

        public ObservableCollection<User> Users { get; } = new();
        public List<string> Roles { get; } = new() { "Admin", "Accountant", "Viewer" };

        public User? Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value != null) { Edit = Copy(value); NewPassword = ""; IsEditing = true; } }
        }

        public User Edit { get => _edit; set => Set(ref _edit, value); }
        public string NewPassword { get => _newPassword; set => Set(ref _newPassword, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public bool IsEditing { get => _isEditing; set => Set(ref _isEditing, value); }

        public ICommand NewUserCommand { get; }
        public ICommand SaveUserCommand { get; }
        public ICommand CancelEditCommand { get; }

        public UserManagementViewModel()
        {
            NewUserCommand = new RelayCommand(() =>
            {
                Selected = null;
                Edit = new User { Role = "Viewer", IsActive = true };
                NewPassword = "";
                StatusMessage = "";
                IsEditing = true;
            });
            SaveUserCommand = new RelayCommand(Save, () => IsEditing);
            CancelEditCommand = new RelayCommand(() =>
            {
                Selected = null;
                Edit = new User { Role = "Viewer", IsActive = true };
                NewPassword = "";
                StatusMessage = "";
                IsEditing = false;
            });
            Load();
        }

        public void Load()
        {
            Users.Clear();
            foreach (var u in AuthService.GetAllUsers()) Users.Add(u);
        }

        // Returns null if the password meets policy, otherwise an error message.
        static string? CheckPasswordStrength(string pwd)
        {
            if (pwd.Length < 8) return "Password must be at least 8 characters.";
            if (!pwd.Any(char.IsLetter) || !pwd.Any(char.IsDigit)) return "Password must contain both letters and numbers.";
            return null;
        }

        void Save()
        {
            bool isNew = Edit.Id == 0;
            if (string.IsNullOrWhiteSpace(Edit.FullName)) { StatusMessage = "Full name is required."; return; }
            if (string.IsNullOrWhiteSpace(Edit.Username)) { StatusMessage = "Username is required."; return; }
            if (string.IsNullOrWhiteSpace(Edit.Role)) { StatusMessage = "Role is required."; return; }
            if (!string.IsNullOrWhiteSpace(Edit.Email) && !Edit.Email.Contains('@')) { StatusMessage = "Email address is not valid."; return; }
            if (AuthService.UsernameExists(Edit.Username, Edit.Id)) { StatusMessage = $"Username '{Edit.Username}' is already taken."; return; }
            if (AuthService.EmailExists(Edit.Email, Edit.Id)) { StatusMessage = $"Email '{Edit.Email}' is already in use."; return; }

            // Password: required for new users; optional (keep current) when editing.
            if (isNew && string.IsNullOrWhiteSpace(NewPassword)) { StatusMessage = "Password is required for a new user."; return; }
            if (!string.IsNullOrWhiteSpace(NewPassword)) { var pe = CheckPasswordStrength(NewPassword); if (pe != null) { StatusMessage = pe; return; } }

            // Protect the last active administrator from being demoted or deactivated.
            if (!isNew)
            {
                var original = Users.FirstOrDefault(u => u.Id == Edit.Id);
                bool wasActiveAdmin = original != null && original.Role == "Admin" && original.IsActive;
                bool losingAdmin = wasActiveAdmin && (Edit.Role != "Admin" || !Edit.IsActive);
                if (losingAdmin && AuthService.ActiveAdminCount(Edit.Id) == 0)
                { StatusMessage = "Cannot demote or deactivate the last active administrator."; return; }
            }

            AuthService.SaveUser(Edit, string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword);
            Load();
            Selected = null;
            Edit = new User { Role = "Viewer", IsActive = true };
            NewPassword = "";
            IsEditing = false;
            StatusMessage = "User saved.";
        }

        static User Copy(User u) => new()
        {
            Id = u.Id, Username = u.Username, FullName = u.FullName,
            Email = u.Email, Role = u.Role, IsActive = u.IsActive
        };
    }
}
