using System.Collections.ObjectModel;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class UserManagementViewModel : BaseViewModel
    {
        ObservableCollection<User> _users=new(); User? _sel; User _edit=new(); string _newPwd="",_status=""; bool _isEditing;
        public ObservableCollection<User> Users{get=>_users;set=>Set(ref _users,value);}
        public User? Selected{get=>_sel;set{Set(ref _sel,value);if(value!=null)Edit=Copy(value);}}
        public User Edit{get=>_edit;set=>Set(ref _edit,value);}
        public string NewPassword{get=>_newPwd;set=>Set(ref _newPwd,value);}
        public string StatusMessage{get=>_status;set=>Set(ref _status,value);}
        public bool IsEditing{get=>_isEditing;set=>Set(ref _isEditing,value);}
        public List<string> Roles{get;}=new(){"Admin","Accountant","Viewer"};
        public ICommand NewUserCommand{get;}
        public ICommand SaveUserCommand{get;}
        public ICommand CancelEditCommand{get;}
        public UserManagementViewModel(){NewUserCommand=new RelayCommand(()=>{Edit=new User{Role="Viewer",IsActive=true};NewPassword="";IsEditing=true;});SaveUserCommand=new RelayCommand(Save,()=>IsEditing);CancelEditCommand=new RelayCommand(()=>IsEditing=false);}
        public void Load(){Users=new ObservableCollection<User>(AuthService.GetAllUsers());}

        // Returns null if password meets policy, otherwise an error message.
        static string? CheckPasswordStrength(string pwd)
        {
            if(pwd.Length<8) return "Password must be at least 8 characters.";
            if(!pwd.Any(char.IsLetter)||!pwd.Any(char.IsDigit)) return "Password must contain both letters and numbers.";
            return null;
        }

        void Save()
        {
            bool isNew = Edit.Id==0;
            if(string.IsNullOrWhiteSpace(Edit.FullName)){StatusMessage="Full name is required.";return;}
            if(string.IsNullOrWhiteSpace(Edit.Username)){StatusMessage="Username is required.";return;}
            if(string.IsNullOrWhiteSpace(Edit.Role)){StatusMessage="Role is required.";return;}
            if(!string.IsNullOrWhiteSpace(Edit.Email)&&!Edit.Email.Contains('@')){StatusMessage="Email address is not valid.";return;}
            if(AuthService.UsernameExists(Edit.Username,Edit.Id)){StatusMessage=$"Username '{Edit.Username}' is already taken.";return;}
            if(AuthService.EmailExists(Edit.Email,Edit.Id)){StatusMessage=$"Email '{Edit.Email}' is already in use.";return;}

            // Password: required for new users; optional (keep current) when editing.
            if(isNew && string.IsNullOrWhiteSpace(NewPassword)){StatusMessage="Password is required for a new user.";return;}
            if(!string.IsNullOrWhiteSpace(NewPassword)){var pe=CheckPasswordStrength(NewPassword);if(pe!=null){StatusMessage=pe;return;}}

            // Protect the last active administrator from being demoted or deactivated.
            if(!isNew)
            {
                var original=Users.FirstOrDefault(u=>u.Id==Edit.Id);
                bool wasActiveAdmin = original!=null && original.Role=="Admin" && original.IsActive;
                bool losingAdmin = wasActiveAdmin && (Edit.Role!="Admin" || !Edit.IsActive);
                if(losingAdmin && AuthService.ActiveAdminCount(Edit.Id)==0)
                {StatusMessage="Cannot demote or deactivate the last active administrator.";return;}
            }

            AuthService.SaveUser(Edit,string.IsNullOrWhiteSpace(NewPassword)?null:NewPassword);
            Load();IsEditing=false;StatusMessage="User saved.";
        }
        User Copy(User u)=>new(){Id=u.Id,Username=u.Username,FullName=u.FullName,Email=u.Email,Role=u.Role,IsActive=u.IsActive};
    }
}
