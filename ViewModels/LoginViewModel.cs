using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Services;

namespace eCheque.MICO360.ViewModels
{
    public class LoginViewModel : BaseViewModel
    {
        string _username = "", _error = "", _email = "", _otpCode = "", _status = "";
        bool _loading, _otpMode, _otpSent, _rememberMe = true;

        /// <summary>Keep the user signed in across restarts (auto sign-out only after 30 days of no use).</summary>
        public bool RememberMe { get => _rememberMe; set => Set(ref _rememberMe, value); }

        public string Username { get => _username; set => Set(ref _username, value); }
        public string ErrorMessage { get => _error; set => Set(ref _error, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public bool IsLoading { get => _loading; set => Set(ref _loading, value); }

        // OTP state
        public string Email { get => _email; set => Set(ref _email, value); }
        public string OtpCode { get => _otpCode; set => Set(ref _otpCode, value); }
        public bool OtpMode { get => _otpMode; set { Set(ref _otpMode, value); OnPropertyChanged(nameof(PasswordMode)); } }
        public bool PasswordMode => !_otpMode;
        public bool OtpSent { get => _otpSent; set => Set(ref _otpSent, value); }

        public ICommand LoginCommand { get; }
        public ICommand UseOtpCommand { get; }
        public ICommand UsePasswordCommand { get; }
        public ICommand SendOtpCommand { get; }
        public ICommand VerifyOtpCommand { get; }
        public event Action? LoginSuccessful;

        public LoginViewModel()
        {
            LoginCommand      = new RelayCommand<string>(DoLogin);
            UseOtpCommand     = new RelayCommand(() => { OtpMode = true;  ResetOtp(); });
            UsePasswordCommand= new RelayCommand(() => { OtpMode = false; ResetOtp(); });
            SendOtpCommand    = new RelayCommand(async () => await SendOtp());
            VerifyOtpCommand  = new RelayCommand(VerifyOtp);
        }

        // Password login (single central login — no company selection).
        public void DoLogin(string? password)
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
            { ErrorMessage = "Please enter username and password."; return; }

            IsLoading = true;
            try
            {
                var error = AuthService.Login(Username.Trim(), password);
                if (error != null) { ErrorMessage = error; return; }
                CompleteLogin();
            }
            finally { IsLoading = false; }
        }

        void ResetOtp() { Email = ""; OtpCode = ""; OtpSent = false; ErrorMessage = ""; StatusMessage = ""; }

        async Task SendOtp()
        {
            if (IsLoading) return;                       // ignore rapid double-clicks
            ErrorMessage = ""; StatusMessage = "";
            if (string.IsNullOrWhiteSpace(Email)) { ErrorMessage = "Please enter your email."; return; }
            IsLoading = true;
            StatusMessage = "Sending code…";
            try
            {
                var error = await AuthService.RequestEmailOtpAsync(Email);
                if (error != null) { ErrorMessage = error; StatusMessage = ""; return; }
                OtpSent = true;
                StatusMessage = "A login code has been emailed to you. Enter it below.";
            }
            catch (Exception ex) { ErrorMessage = $"Could not send the code: {ex.Message}"; StatusMessage = ""; }
            finally { IsLoading = false; }
        }

        void VerifyOtp()
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(OtpCode)) { ErrorMessage = "Enter the code from your email."; return; }
            var error = AuthService.VerifyEmailOtp(Email, OtpCode);
            if (error != null) { ErrorMessage = error; return; }
            CompleteLogin();
        }

        // Shared post-authentication step for both password and OTP paths.
        void CompleteLogin()
        {
            var company = CompanyService.GetAll().FirstOrDefault();
            if (company == null) { ErrorMessage = "No company is configured. Contact your administrator."; AuthService.Logout(); return; }
            CompanyService.OpenCompany(company.Id, company.Name);
            // Remember me: persist the session so the app auto-signs-in next launch (30-day inactivity expiry).
            if (RememberMe && AuthService.CurrentUser != null) SessionService.Remember(AuthService.CurrentUser.Id);
            else SessionService.Clear();
            LoginSuccessful?.Invoke();
        }
    }
}
