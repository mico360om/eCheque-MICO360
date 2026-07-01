using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        string _username = "", _error = "", _email = "", _otpCode = "", _status = "";
        bool _otpMode, _otpSent;

        public string Username { get => _username; set => Set(ref _username, value); }
        public string ErrorMessage { get => _error; set => Set(ref _error, value); }
        public string StatusMessage { get => _status; set => Set(ref _status, value); }
        public string Email { get => _email; set => Set(ref _email, value); }
        public string OtpCode { get => _otpCode; set => Set(ref _otpCode, value); }
        public bool OtpMode { get => _otpMode; set { Set(ref _otpMode, value); OnPropertyChanged(nameof(PasswordMode)); } }
        public bool PasswordMode => !_otpMode;
        public bool OtpSent { get => _otpSent; set => Set(ref _otpSent, value); }

        public ICommand UseOtpCommand { get; }
        public ICommand UsePasswordCommand { get; }
        public ICommand SendOtpCommand { get; }
        public ICommand VerifyOtpCommand { get; }
        public event Action? LoginSuccessful;

        public LoginViewModel()
        {
            UseOtpCommand      = new RelayCommand(() => { OtpMode = true; ErrorMessage = ""; StatusMessage = ""; });
            UsePasswordCommand = new RelayCommand(() => { OtpMode = false; OtpSent = false; ErrorMessage = ""; StatusMessage = ""; });
            SendOtpCommand     = new RelayCommand(async () => await SendOtp());
            VerifyOtpCommand   = new RelayCommand(VerifyOtp);
        }

        // Password login — called from the view (password comes from the PasswordBox).
        public void DoLogin(string? password)
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(Username)) { ErrorMessage = "Please enter your username."; return; }
            var error = AuthService.Login(Username.Trim(), password ?? "");
            if (error != null) { ErrorMessage = error; return; }
            CompleteLogin();
        }

        async Task SendOtp()
        {
            ErrorMessage = ""; StatusMessage = "";
            if (string.IsNullOrWhiteSpace(Email)) { ErrorMessage = "Please enter your email."; return; }
            StatusMessage = "Sending code…";
            var error = await AuthService.RequestEmailOtpAsync(Email);
            if (error != null) { ErrorMessage = error; StatusMessage = ""; return; }
            OtpSent = true;
            StatusMessage = "A login code has been emailed to you. Enter it below.";
        }

        void VerifyOtp()
        {
            ErrorMessage = "";
            if (string.IsNullOrWhiteSpace(OtpCode)) { ErrorMessage = "Enter the code from your email."; return; }
            var error = AuthService.VerifyEmailOtp(Email, OtpCode);
            if (error != null) { ErrorMessage = error; return; }
            CompleteLogin();
        }

        void CompleteLogin()
        {
            var company = CompanyService.GetAll().FirstOrDefault();
            if (company == null) { ErrorMessage = "No company is configured. Contact your administrator."; AuthService.Logout(); return; }
            CompanyService.OpenCompany(company.Id, company.Name);
            LoginSuccessful?.Invoke();
        }
    }
}
