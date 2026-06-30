using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public enum LegalKind { Terms, Privacy }

    /// <summary>Backs the Terms &amp; Conditions and Privacy Policy pages — content is editable by admins and stored in the DB.</summary>
    public class LegalViewModel : BaseViewModel
    {
        readonly string _contentKey, _updatedKey, _auditName;
        string _title = "", _content = "", _lastUpdated = "", _status = "";
        bool _isEditing;

        public string Title       { get => _title;       set => Set(ref _title, value); }
        public string Content     { get => _content;     set => Set(ref _content, value); }
        public string LastUpdated { get => _lastUpdated; set => Set(ref _lastUpdated, value); }
        public string StatusMessage { get => _status;    set => Set(ref _status, value); }
        public bool IsEditing     { get => _isEditing;   set { Set(ref _isEditing, value); OnPropertyChanged(nameof(IsViewing)); OnPropertyChanged(nameof(ShowEdit)); } }
        public bool IsViewing     => !_isEditing;
        public bool IsAdmin       => AuthService.IsAdmin;
        public bool ShowEdit      => IsAdmin && !_isEditing;

        public ICommand EditCommand   { get; }
        public ICommand SaveCommand   { get; }
        public ICommand CancelCommand { get; }
        public ICommand BackCommand   { get; }

        public event Action? BackRequested;

        public LegalViewModel(LegalKind kind)
        {
            if (kind == LegalKind.Terms)
            {
                Title = "Terms & Conditions";
                _contentKey = "Legal_Terms_Content"; _updatedKey = "Legal_Terms_Updated"; _auditName = "Terms & Conditions";
            }
            else
            {
                Title = "Privacy Policy";
                _contentKey = "Legal_Privacy_Content"; _updatedKey = "Legal_Privacy_Updated"; _auditName = "Privacy Policy";
            }

            EditCommand   = new Helpers.RelayCommand(() => { IsEditing = true; StatusMessage = ""; }, () => IsAdmin);
            SaveCommand   = new Helpers.RelayCommand(Save);
            CancelCommand = new Helpers.RelayCommand(() => { Load(); IsEditing = false; StatusMessage = ""; });
            BackCommand   = new Helpers.RelayCommand(() => BackRequested?.Invoke());
        }

        public void Load()
        {
            Content     = DatabaseService.GetSetting(_contentKey, "");
            LastUpdated = DatabaseService.GetSetting(_updatedKey, "");
        }

        void Save()
        {
            DatabaseService.SaveSetting(_contentKey, Content);
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            DatabaseService.SaveSetting(_updatedKey, today);
            LastUpdated = today;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Legal Content Updated", _auditName);
            IsEditing = false;
            StatusMessage = "Saved.";
        }
    }
}
