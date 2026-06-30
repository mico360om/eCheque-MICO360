using System.Diagnostics;
using System.Net.Http;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public class UpdateViewModel : BaseViewModel
    {
        string _status = "", _currentVersion = AppInfo.Version, _latestVersion = "", _changelog = "", _sizeDisplay = "";
        bool _busy, _updateAvailable, _mandatory, _checked;
        double _progress;
        UpdateInfo? _info;

        public string CurrentVersion { get => _currentVersion; set => Set(ref _currentVersion, value); }
        public string LatestVersion  { get => _latestVersion;  set => Set(ref _latestVersion, value); }
        public string Changelog      { get => _changelog;      set => Set(ref _changelog, value); }
        public string SizeDisplay    { get => _sizeDisplay;    set => Set(ref _sizeDisplay, value); }
        public string StatusMessage  { get => _status;         set => Set(ref _status, value); }
        public double Progress       { get => _progress;       set => Set(ref _progress, value); }
        public bool IsBusy           { get => _busy;           set { Set(ref _busy, value); OnPropertyChanged(nameof(IsNotBusy)); } }
        public bool IsNotBusy        => !_busy;
        public bool UpdateAvailable  { get => _updateAvailable;set => Set(ref _updateAvailable, value); }
        public bool IsMandatory      { get => _mandatory;      set => Set(ref _mandatory, value); }
        public bool HasChecked       { get => _checked;        set => Set(ref _checked, value); }

        public ICommand CheckCommand   { get; }
        public ICommand InstallCommand { get; }
        public ICommand ViewLogCommand { get; }

        public UpdateViewModel()
        {
            CheckCommand   = new RelayCommand(async () => await CheckAsync());
            InstallCommand = new RelayCommand(async () => await DownloadAndInstallAsync(), () => UpdateAvailable && IsNotBusy);
            ViewLogCommand = new RelayCommand(OpenLog);
        }

        public async Task CheckAsync()
        {
            if (IsBusy) return;
            IsBusy = true; Progress = 0; StatusMessage = "Checking for updates…";
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
                StatusMessage   = _info.UpdateAvailable
                    ? (_info.Mandatory ? "A required update is available." : "An update is available.")
                    : "You are running the latest version.";
            }
            catch (HttpRequestException)
            {
                StatusMessage = "No internet connection or the update server is unreachable.";
                UpdateService.Log("Check failed: network error.");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Update check failed: {ex.Message}";
                UpdateService.Log($"Check failed: {ex.Message}");
            }
            finally { IsBusy = false; }
        }

        public async Task DownloadAndInstallAsync()
        {
            if (_info == null || !_info.UpdateAvailable || IsBusy) return;
            IsBusy = true; Progress = 0;
            try
            {
                StatusMessage = "Downloading update…";
                var progress = new Progress<double>(p => Progress = p * 100);
                var file = await UpdateService.DownloadAsync(_info, progress, CancellationToken.None);

                StatusMessage = "Verifying update…";
                if (!UpdateService.VerifyChecksum(file, _info.Sha256))
                {
                    StatusMessage = "Verification failed — the update file is corrupted. Installation aborted.";
                    UpdateService.Log($"FAILED: checksum mismatch. {_info.CurrentVersion} -> {_info.LatestVersion}");
                    IsBusy = false;
                    return;
                }

                UpdateService.Log($"SUCCESS DOWNLOAD: {_info.CurrentVersion} -> {_info.LatestVersion} verified. Awaiting restart.");
                StatusMessage = "Update ready. The application will now restart to finish installing.";

                if (System.Windows.MessageBox.Show(
                        $"Update {_info.LatestVersion} downloaded and verified.\n\nThe app will close and restart to apply it. Your data, settings and records are kept safe.\n\nContinue?",
                        "Install Update", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Information)
                    != System.Windows.MessageBoxResult.OK)
                {
                    StatusMessage = "Installation postponed. It will apply next time you choose to update.";
                    IsBusy = false;
                    return;
                }

                UpdateService.ApplyAndRestart(file);
                System.Windows.Application.Current.Shutdown();
            }
            catch (HttpRequestException)
            {
                StatusMessage = "Download failed — check your internet connection and try again.";
                UpdateService.Log("FAILED: download network error.");
                IsBusy = false;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Update failed: {ex.Message}";
                UpdateService.Log($"FAILED: {ex.Message}");
                IsBusy = false;
            }
        }

        void OpenLog()
        {
            try
            {
                if (System.IO.File.Exists(UpdateService.LogPath))
                    Process.Start(new ProcessStartInfo(UpdateService.LogPath) { UseShellExecute = true });
                else
                    StatusMessage = "No update log yet.";
            }
            catch (Exception ex) { StatusMessage = $"Could not open log: {ex.Message}"; }
        }
    }
}
