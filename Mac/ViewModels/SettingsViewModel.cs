using System.Collections.Generic;
using System.Windows.Input;
using eCheque.MICO360.Core;
using eCheque.MICO360.Core.Services;

namespace eCheque.MICO360.Mac.ViewModels
{
    /// <summary>App settings — mirrors the Windows Settings screen (company, amount-in-words, Mailjet, paths, backup).</summary>
    public class SettingsViewModel : ViewModelBase
    {
        const decimal PreviewAmount = 4956.250m;

        string _co = "", _cur = "OMR", _df = "dd/MM/yyyy", _pdf = "", _bak = "", _cf = "UPPERCASE", _status = "";
        string _currencyWording = "Omani Rials", _baisaWording = "Baisa";
        bool _includeBaisa = true, _addOnly = true;
        string _mjKey = "", _mjSecret = "", _mjFrom = "", _mjFromName = "eCheque MICO360";
        string _wordsPreview = "";

        public string CompanyName     { get => _co;  set => Set(ref _co, value); }
        public string Currency        { get => _cur; set => Set(ref _cur, value); }
        public string DateFormat      { get => _df;  set => Set(ref _df, value); }
        public string PdfPath         { get => _pdf; set => Set(ref _pdf, value); }
        public string BackupPath      { get => _bak; set => Set(ref _bak, value); }

        public string CaseFormat      { get => _cf; set { if (Set(ref _cf, value)) UpdatePreview(); } }
        public string CurrencyWording { get => _currencyWording; set { if (Set(ref _currencyWording, value)) UpdatePreview(); } }
        public string BaisaWording    { get => _baisaWording;    set { if (Set(ref _baisaWording, value)) UpdatePreview(); } }
        public bool   IncludeBaisa    { get => _includeBaisa;    set { if (Set(ref _includeBaisa, value)) UpdatePreview(); } }
        public bool   AddOnly         { get => _addOnly;         set { if (Set(ref _addOnly, value)) UpdatePreview(); } }

        public string MailjetApiKey    { get => _mjKey;      set => Set(ref _mjKey, value); }
        public string MailjetSecretKey { get => _mjSecret;   set => Set(ref _mjSecret, value); }
        public string MailjetFromEmail { get => _mjFrom;     set => Set(ref _mjFrom, value); }
        public string MailjetFromName  { get => _mjFromName; set => Set(ref _mjFromName, value); }

        public string StatusMessage { get => _status;       set => Set(ref _status, value); }
        public string WordsPreview  { get => _wordsPreview; set => Set(ref _wordsPreview, value); }

        public List<string> CaseFormats { get; } = new() { "UPPERCASE", "TitleCase", "lowercase" };
        public List<string> DateFormats { get; } = new() { "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd-MMM-yyyy" };

        public ICommand SaveCommand    { get; }
        public ICommand BackupCommand  { get; }
        public ICommand OpenLogCommand { get; }

        public SettingsViewModel()
        {
            SaveCommand    = new RelayCommand(Save);
            BackupCommand  = new RelayCommand(DoBackup);
            OpenLogCommand = new RelayCommand(BugReportService.OpenLog);
        }

        public void Load()
        {
            CompanyName     = DatabaseService.GetSetting("CompanyName", "My Company LLC");
            Currency        = DatabaseService.GetSetting("DefaultCurrency", "OMR");
            DateFormat      = DatabaseService.GetSetting("DateFormat", "dd/MM/yyyy");
            PdfPath         = DatabaseService.GetSetting("PdfSavePath", "");
            BackupPath      = DatabaseService.GetSetting("BackupPath", "");
            CaseFormat      = DatabaseService.GetSetting("AmountCaseFormat", "UPPERCASE");
            CurrencyWording = DatabaseService.GetSetting("AmountCurrencyWording", "Omani Rials");
            BaisaWording    = DatabaseService.GetSetting("AmountBaisaWording", "Baisa");
            IncludeBaisa    = DatabaseService.GetSetting("AmountIncludeBaisa", "true") == "true";
            AddOnly         = DatabaseService.GetSetting("AmountAddOnly", "true") == "true";

            MailjetApiKey    = CompanyService.GetMasterSetting("Mailjet_ApiKey", "");
            MailjetSecretKey = CompanyService.GetMasterSetting("Mailjet_SecretKey", "");
            MailjetFromEmail = CompanyService.GetMasterSetting("Mailjet_FromEmail", "");
            MailjetFromName  = CompanyService.GetMasterSetting("Mailjet_FromName", "eCheque MICO360");

            UpdatePreview();
        }

        void UpdatePreview()
        {
            WordsPreview = AmountToWordsService.Convert(
                PreviewAmount, CaseFormat, CurrencyWording, BaisaWording, IncludeBaisa, AddOnly);
        }

        void Save()
        {
            if (!string.IsNullOrWhiteSpace(PdfPath) && !System.IO.Directory.Exists(PdfPath))
            { StatusMessage = $"PDF output path does not exist: {PdfPath}"; return; }
            if (!string.IsNullOrWhiteSpace(BackupPath) && !System.IO.Directory.Exists(BackupPath))
            { StatusMessage = $"Backup path does not exist: {BackupPath}"; return; }

            DatabaseService.SaveSetting("CompanyName", CompanyName);
            DatabaseService.SaveSetting("DefaultCurrency", Currency);
            DatabaseService.SaveSetting("DateFormat", DateFormat);
            DatabaseService.SaveSetting("PdfSavePath", PdfPath);
            DatabaseService.SaveSetting("BackupPath", BackupPath);
            DatabaseService.SaveSetting("AmountCaseFormat", CaseFormat);
            DatabaseService.SaveSetting("AmountCurrencyWording", CurrencyWording);
            DatabaseService.SaveSetting("AmountBaisaWording", BaisaWording);
            DatabaseService.SaveSetting("AmountIncludeBaisa", IncludeBaisa ? "true" : "false");
            DatabaseService.SaveSetting("AmountAddOnly", AddOnly ? "true" : "false");

            CompanyService.SetMasterSetting("Mailjet_ApiKey", (MailjetApiKey ?? "").Trim());
            CompanyService.SetMasterSetting("Mailjet_SecretKey", (MailjetSecretKey ?? "").Trim());
            CompanyService.SetMasterSetting("Mailjet_FromEmail", (MailjetFromEmail ?? "").Trim());
            CompanyService.SetMasterSetting("Mailjet_FromName", (MailjetFromName ?? "").Trim());

            DatabaseService.LogAudit(AuthService.CurrentUser?.Username ?? "", "Settings Changed", "",
                $"Company={CompanyName}, Currency={Currency}, CaseFormat={CaseFormat}");
            StatusMessage = "Settings saved successfully.";
        }

        void DoBackup()
        {
            try
            {
                var p = BackupService.CreateBackup();
                StatusMessage = $"Backup created: {System.IO.Path.GetFileName(p)}";
            }
            catch (System.Exception ex)
            {
                StatusMessage = $"Backup failed: {ex.Message}";
            }
        }
    }
}
