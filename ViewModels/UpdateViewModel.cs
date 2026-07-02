using System.Diagnostics;
using System.Net.Http;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
using Brush = System.Windows.Media.Brush;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;

namespace eCheque.MICO360.ViewModels
{
    public class UpdateViewModel : BaseViewModel
    {
        // Status colours: neutral for info/in-progress, green for good news, red ONLY for real errors.
        static readonly Brush InfoBrush    = Freeze(0x44, 0x44, 0x44);
        static readonly Brush SuccessBrush = Freeze(0x2E, 0x7D, 0x32);
        static readonly Brush ErrorBrush   = Freeze(0xC0, 0x39, 0x2B);
        static readonly Brush BrandBrush   = Freeze(0x8B, 0x18, 0x18);
        static readonly Brush NeutralDark  = Freeze(0x1A, 0x1A, 0x1A);
        static Brush Freeze(byte r, byte g, byte b) { var br = new SolidColorBrush(Color.FromRgb(r, g, b)); br.Freeze(); return br; }

        string _status = "", _currentVersion = AppInfo.Version, _latestVersion = "", _changelog = "", _sizeDisplay = "", _statusKind = "info";
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
        public bool UpdateAvailable  { get => _updateAvailable;set { Set(ref _updateAvailable, value); OnPropertyChanged(nameof(LatestVersionBrush)); OnPropertyChanged(nameof(UpToDate)); } }
        public bool IsMandatory      { get => _mandatory;      set => Set(ref _mandatory, value); }
        public bool HasChecked       { get => _checked;        set { Set(ref _checked, value); OnPropertyChanged(nameof(LatestVersionBrush)); OnPropertyChanged(nameof(UpToDate)); } }

        /// <summary>True only after a check that found no newer version — drives the green "up to date" chip.</summary>
        public bool UpToDate => _checked && !_updateAvailable;

        public Brush StatusBrush => _statusKind switch { "success" => SuccessBrush, "error" => ErrorBrush, _ => InfoBrush };
        public Brush LatestVersionBrush => !_checked ? NeutralDark : (_updateAvailable ? BrandBrush : SuccessBrush);

        public ICommand CheckCommand   { get; }
        public ICommand InstallCommand { get; }
        public ICommand ViewLogCommand { get; }

        public UpdateViewModel()
        {
            CheckCommand   = new RelayCommand(async () => await CheckAsync());
            InstallCommand = new RelayCommand(async () => await DownloadAndInstallAsync(), () => UpdateAvailable && IsNotBusy);
            ViewLogCommand = new RelayCommand(OpenLog);
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
            IsBusy = true; Progress = 0; SetStatus("Checking for updates…", "info");
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
                    SetStatus(_info.Mandatory ? "A required update is available." : "An update is available.", "info");
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

        public async Task DownloadAndInstallAsync()
        {
            if (_info == null || !_info.UpdateAvailable || IsBusy) return;
            IsBusy = true; Progress = 0;
            try
            {
                SetStatus("Downloading update…", "info");
                var progress = new Progress<double>(p => Progress = p * 100);
                var file = await UpdateService.DownloadAsync(_info, progress, CancellationToken.None);

                SetStatus("Verifying update…", "info");
                if (!UpdateService.VerifyChecksum(file, _info.Sha256))
                {
                    SetStatus("Verification failed — the update file is corrupted. Installation aborted.", "error");
                    UpdateService.Log($"FAILED: checksum mismatch. {_info.CurrentVersion} -> {_info.LatestVersion}");
                    IsBusy = false;
                    return;
                }

                UpdateService.Log($"SUCCESS DOWNLOAD: {_info.CurrentVersion} -> {_info.LatestVersion} verified. Awaiting restart.");
                SetStatus("Update ready. The application will now restart to finish installing.", "success");

                if (System.Windows.MessageBox.Show(
                        $"Update {_info.LatestVersion} downloaded and verified.\n\nThe app will close and the installer will run to apply it (you may see a Windows admin prompt). Your data, settings and records are kept safe.\n\nContinue?",
                        "Install Update", System.Windows.MessageBoxButton.OKCancel, System.Windows.MessageBoxImage.Information)
                    != System.Windows.MessageBoxResult.OK)
                {
                    SetStatus("Installation postponed. It will apply next time you choose to update.", "info");
                    IsBusy = false;
                    return;
                }

                UpdateService.ApplyAndRestart(file);
                System.Windows.Application.Current.Shutdown();
            }
            catch (HttpRequestException)
            {
                SetStatus("Download failed — check your internet connection and try again.", "error");
                UpdateService.Log("FAILED: download network error.");
                IsBusy = false;
            }
            catch (Exception ex)
            {
                SetStatus($"Update failed: {ex.Message}", "error");
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
                    SetStatus("No update log yet.", "info");
            }
            catch (Exception ex) { SetStatus($"Could not open log: {ex.Message}", "error"); }
        }
    }
}
