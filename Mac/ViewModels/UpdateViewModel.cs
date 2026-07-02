using System.Net.Http;
using System.Diagnostics;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Models;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class UpdateViewModel : ViewModelBase
    {
        string _currentVersion = AppInfo.Version, _latestVersion = "—", _changelog = "", _sizeDisplay = "—", _status = "", _statusKind = "info";
        bool _busy, _updateAvailable, _mandatory, _hasChecked;
        UpdateInfo? _info;

        public string CurrentVersion { get => _currentVersion; set => Set(ref _currentVersion, value); }
        public string LatestVersion  { get => _latestVersion;  set => Set(ref _latestVersion, value); }
        public string Changelog      { get => _changelog;      set => Set(ref _changelog, value); }
        public string SizeDisplay    { get => _sizeDisplay;    set => Set(ref _sizeDisplay, value); }
        public string StatusMessage  { get => _status;         set => Set(ref _status, value); }

        public bool IsBusy
        {
            get => _busy;
            set { if (Set(ref _busy, value)) OnPropertyChanged(nameof(IsNotBusy)); }
        }
        public bool IsNotBusy => !_busy;

        public bool UpdateAvailable
        {
            get => _updateAvailable;
            set { if (Set(ref _updateAvailable, value)) { OnPropertyChanged(nameof(UpToDate)); OnPropertyChanged(nameof(LatestVersionBrush)); } }
        }
        public bool IsMandatory { get => _mandatory; set => Set(ref _mandatory, value); }

        public bool HasChecked
        {
            get => _hasChecked;
            set { if (Set(ref _hasChecked, value)) { OnPropertyChanged(nameof(UpToDate)); OnPropertyChanged(nameof(LatestVersionBrush)); } }
        }

        /// <summary>True only after a check that found no newer version — drives the green "up to date" chip.</summary>
        public bool UpToDate => _hasChecked && !_updateAvailable;

        // Status colour: green = good news, red = real error, grey = info/in-progress.
        public string StatusBrush => _statusKind switch { "success" => "#2E7D32", "error" => "#C0392B", _ => "#444444" };
        // Latest version colour: neutral until checked, brand-red when an update exists, green when up to date.
        public string LatestVersionBrush => !_hasChecked ? "#1A1A1A" : (_updateAvailable ? "#8B1818" : "#2E7D32");

        // Public repository so users can always update manually. macOS has NO in-app installer — link only.
        public string RepoUrl     => AppInfo.RepoUrl;
        public string ReleasesUrl => AppInfo.RepoUrl + "/releases/latest";

        public ICommand CheckCommand        { get; }
        public ICommand OpenReleasesCommand { get; }
        public ICommand OpenRepoCommand     { get; }

        public UpdateViewModel()
        {
            CheckCommand        = new RelayCommand(async () => await CheckAsync());
            OpenReleasesCommand = new RelayCommand(() => OpenUrl(ReleasesUrl));
            OpenRepoCommand     = new RelayCommand(() => OpenUrl(RepoUrl));
        }

        void SetStatus(string message, string kind = "info")
        {
            _statusKind = kind;
            StatusMessage = message;
            OnPropertyChanged(nameof(StatusBrush));
        }

        public async Task CheckAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            SetStatus("Checking for updates…", "info");
            try
            {
                _info = await UpdateService.CheckForUpdatesAsync();
                CurrentVersion  = _info.CurrentVersion;
                LatestVersion   = string.IsNullOrEmpty(_info.LatestVersion) ? "—" : _info.LatestVersion;
                Changelog       = _info.Changelog;
                SizeDisplay     = _info.SizeDisplay;
                UpdateAvailable = _info.UpdateAvailable;
                IsMandatory     = _info.Mandatory;
                HasChecked      = true;

                if (_info.UpdateAvailable)
                    SetStatus(_info.Mandatory
                        ? "A required update is available. Download it from GitHub below."
                        : "An update is available. Download it from GitHub below.", "info");
                else
                    SetStatus("✔ You are running the latest version.", "success");
            }
            catch (HttpRequestException)
            {
                SetStatus("No internet connection or the update server is unreachable.", "error");
                UpdateService.Log("Check failed: network error.");
            }
            catch (Exception ex)
            {
                SetStatus($"Update check failed: {ex.Message}", "error");
                UpdateService.Log($"Check failed: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { SetStatus($"Could not open the link: {ex.Message}", "error"); }
        }

        public void Load() => _ = CheckAsync();
    }
}
