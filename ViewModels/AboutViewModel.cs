using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        string _intro = "", _lastUpdated = "", _status = "";
        bool _isEditing;

        public string AppName      => AppInfo.AppName;
        public string CompanyName  => AppInfo.CompanyName;
        public string ContactEmail => AppInfo.ContactEmail;
        public string Website      => AppInfo.Website;
        public string Version      => $"Version {AppInfo.Version}";

        public string Intro        { get => _intro;       set => Set(ref _intro, value); }
        public string LastUpdated  { get => _lastUpdated; set => Set(ref _lastUpdated, value); }
        public string StatusMessage{ get => _status;      set => Set(ref _status, value); }
        public bool IsEditing      { get => _isEditing;   set { Set(ref _isEditing, value); OnPropertyChanged(nameof(IsViewing)); OnPropertyChanged(nameof(ShowEdit)); } }
        public bool IsViewing      => !_isEditing;
        public bool IsAdmin        => AuthService.IsAdmin;
        public bool ShowEdit       => IsAdmin && !_isEditing;

        public ICommand EditCommand   { get; }
        public ICommand SaveCommand   { get; }
        public ICommand CancelCommand { get; }
        public ICommand BackCommand   { get; }

        public event Action? BackRequested;

        public AboutViewModel()
        {
            EditCommand   = new RelayCommand(() => { IsEditing = true; StatusMessage = ""; }, () => IsAdmin);
            SaveCommand   = new RelayCommand(Save);
            CancelCommand = new RelayCommand(() => { Load(); IsEditing = false; StatusMessage = ""; });
            BackCommand   = new RelayCommand(() => BackRequested?.Invoke());
        }

        public void Load()
        {
            Intro       = DatabaseService.GetSetting("Legal_About_Intro", AppInfo.CompanyIntro);
            LastUpdated = DatabaseService.GetSetting("Legal_About_Updated", "");
        }

        void Save()
        {
            DatabaseService.SaveSetting("Legal_About_Intro", Intro);
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            DatabaseService.SaveSetting("Legal_About_Updated", today);
            LastUpdated = today;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Legal Content Updated", "About Us");
            IsEditing = false;
            StatusMessage = "Saved.";
        }
    }
}
