using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        string _co="",_cur="OMR",_df="dd/MM/yyyy",_pdf="",_bak="",_cf="UPPERCASE",_status="";
        string _currencyWording="Omani Rials",_baisaWording="Baisa"; bool _includeBaisa=true,_addOnly=true;
        string _mjKey="",_mjSecret="",_mjFrom="",_mjFromName="eCheque MICO360";
        bool _pdcEnabled; string _pdcEmail="",_pdcFreq="Weekly",_pdcWa=""; int _pdcLook=7;
        bool _syncEnabled; string _syncUrl="",_syncKey="",_syncStatus="";
        static readonly (string Label,int Days)[] Freqs={("Daily",1),("Every 3 days",3),("Weekly",7),("Every 2 weeks",14),("Monthly",30)};
        static int FreqDays(string label){var m=Freqs.FirstOrDefault(f=>f.Label==label);return m.Days==0?7:m.Days;}
        static string FreqLabel(int days){var m=Freqs.FirstOrDefault(f=>f.Days==days);return m.Label??"Weekly";}
        public bool PdcReminderEnabled{get=>_pdcEnabled;set=>Set(ref _pdcEnabled,value);}
        public string PdcReminderEmail{get=>_pdcEmail;set=>Set(ref _pdcEmail,value);}
        public string PdcWhatsApp{get=>_pdcWa;set=>Set(ref _pdcWa,value);}
        public string PdcFrequency{get=>_pdcFreq;set=>Set(ref _pdcFreq,value);}
        public int PdcLookAheadDays{get=>_pdcLook;set=>Set(ref _pdcLook,value);}
        public List<string> ReminderFrequencies{get;}=new(){"Daily","Every 3 days","Weekly","Every 2 weeks","Monthly"};
        public bool SyncEnabled{get=>_syncEnabled;set=>Set(ref _syncEnabled,value);}
        public string SyncServerUrl{get=>_syncUrl;set=>Set(ref _syncUrl,value);}
        public string SyncOrgKey{get=>_syncKey;set=>Set(ref _syncKey,value);}
        public string SyncStatus{get=>_syncStatus;set=>Set(ref _syncStatus,value);}
        public string MailjetApiKey{get=>_mjKey;set=>Set(ref _mjKey,value);}
        public string MailjetSecretKey{get=>_mjSecret;set=>Set(ref _mjSecret,value);}
        public string MailjetFromEmail{get=>_mjFrom;set=>Set(ref _mjFrom,value);}
        public string MailjetFromName{get=>_mjFromName;set=>Set(ref _mjFromName,value);}
        public string CompanyName{get=>_co;set=>Set(ref _co,value);}
        public string Currency{get=>_cur;set=>Set(ref _cur,value);}
        public string DateFormat{get=>_df;set=>Set(ref _df,value);}
        public string PdfPath{get=>_pdf;set=>Set(ref _pdf,value);}
        public string BackupPath{get=>_bak;set=>Set(ref _bak,value);}
        public string CaseFormat{get=>_cf;set=>Set(ref _cf,value);}
        public string CurrencyWording{get=>_currencyWording;set=>Set(ref _currencyWording,value);}
        public string BaisaWording{get=>_baisaWording;set=>Set(ref _baisaWording,value);}
        public bool IncludeBaisa{get=>_includeBaisa;set=>Set(ref _includeBaisa,value);}
        public bool AddOnly{get=>_addOnly;set=>Set(ref _addOnly,value);}
        public string StatusMessage{get=>_status;set=>Set(ref _status,value);}
        public List<string> CaseFormats{get;}=new(){"UPPERCASE","TitleCase","lowercase"};
        public List<string> DateFormats{get;}=new(){"dd/MM/yyyy","MM/dd/yyyy","yyyy-MM-dd","dd-MMM-yyyy"};
        public ICommand SaveCommand{get;}
        public ICommand BackupCommand{get;}
        public ICommand RestoreCommand{get;}
        public ICommand BrowsePdfCommand{get;}
        public ICommand BrowseBackupCommand{get;}
        public ICommand OpenLogCommand{get;}
        public ICommand SendReminderNowCommand{get;}
        public ICommand SendWhatsAppReminderCommand{get;}
        public ICommand ConnectSyncCommand{get;}
        public ICommand SyncNowCommand{get;}
        public SettingsViewModel(){SaveCommand=new RelayCommand(Save);BackupCommand=new RelayCommand(DoBackup);RestoreCommand=new RelayCommand(DoRestore);BrowsePdfCommand=new RelayCommand(BrowsePdf);BrowseBackupCommand=new RelayCommand(BrowseBackup);OpenLogCommand=new RelayCommand(BugReportService.OpenLog);SendReminderNowCommand=new RelayCommand(async()=>{try{PersistReminder();StatusMessage="Sending reminder…";StatusMessage=await PdcReminderService.SendNowAsync();}catch(Exception ex){StatusMessage="Reminder error: "+ex.Message;}});SendWhatsAppReminderCommand=new RelayCommand(()=>{try{PersistReminder();var(url,msg)=PdcReminderService.BuildWhatsAppReminder();if(url==null){StatusMessage=msg;return;}System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url){UseShellExecute=true});StatusMessage="Opening WhatsApp — review the message and tap Send.";}catch(Exception ex){StatusMessage="WhatsApp error: "+ex.Message;}});
            ConnectSyncCommand=new RelayCommand(async()=>{try{SyncStatus="Connecting…";SyncStatus=await SyncService.RegisterAsync(SyncServerUrl,SyncOrgKey);}catch(Exception ex){SyncStatus="Error: "+ex.Message;}});
            SyncNowCommand=new RelayCommand(async()=>{try{SyncService.ServerUrl=SyncServerUrl;SyncService.Enabled=SyncEnabled;if(!SyncService.IsRegistered){SyncStatus="Connect this PC first (enter URL + organisation key, then Connect).";return;}SyncStatus="Syncing…";var r=await SyncService.SyncOnceAsync();SyncStatus=r.ToString();}catch(Exception ex){SyncStatus="Error: "+ex.Message;}});}
        void PersistReminder(){PdcReminderService.Enabled=PdcReminderEnabled;PdcReminderService.Recipient=PdcReminderEmail;PdcReminderService.WhatsAppNumber=PdcWhatsApp;PdcReminderService.FrequencyDays=FreqDays(PdcFrequency);PdcReminderService.LookAheadDays=PdcLookAheadDays<1?7:PdcLookAheadDays;}
        public void Load(){CompanyName=DatabaseService.GetSetting("CompanyName","My Company LLC");Currency=DatabaseService.GetSetting("DefaultCurrency","OMR");DateFormat=DatabaseService.GetSetting("DateFormat","dd/MM/yyyy");PdfPath=DatabaseService.GetSetting("PdfSavePath","");BackupPath=DatabaseService.GetSetting("BackupPath","");CaseFormat=DatabaseService.GetSetting("AmountCaseFormat","UPPERCASE");CurrencyWording=DatabaseService.GetSetting("AmountCurrencyWording","Omani Rials");BaisaWording=DatabaseService.GetSetting("AmountBaisaWording","Baisa");IncludeBaisa=DatabaseService.GetSetting("AmountIncludeBaisa","true")=="true";AddOnly=DatabaseService.GetSetting("AmountAddOnly","true")=="true";
            MailjetApiKey=CompanyService.GetMasterSetting("Mailjet_ApiKey","");MailjetSecretKey=CompanyService.GetMasterSetting("Mailjet_SecretKey","");MailjetFromEmail=CompanyService.GetMasterSetting("Mailjet_FromEmail","");MailjetFromName=CompanyService.GetMasterSetting("Mailjet_FromName","eCheque MICO360");
            PdcReminderEnabled=PdcReminderService.Enabled;PdcReminderEmail=PdcReminderService.Recipient;PdcWhatsApp=PdcReminderService.WhatsAppNumber;PdcFrequency=FreqLabel(PdcReminderService.FrequencyDays);PdcLookAheadDays=PdcReminderService.LookAheadDays;
            SyncEnabled=SyncService.Enabled;SyncServerUrl=SyncService.ServerUrl;SyncStatus=SyncService.IsRegistered?("This PC is registered. "+SyncService.LastResult):"Not connected. Enter the server URL + organisation key, then Connect.";}
        void Save(){
            if(!string.IsNullOrWhiteSpace(PdfPath)&&!System.IO.Directory.Exists(PdfPath)){StatusMessage=$"PDF output path does not exist: {PdfPath}";return;}
            if(!string.IsNullOrWhiteSpace(BackupPath)&&!System.IO.Directory.Exists(BackupPath)){StatusMessage=$"Backup path does not exist: {BackupPath}";return;}
            DatabaseService.SaveSetting("CompanyName",CompanyName);DatabaseService.SaveSetting("DefaultCurrency",Currency);DatabaseService.SaveSetting("DateFormat",DateFormat);DatabaseService.SaveSetting("PdfSavePath",PdfPath);DatabaseService.SaveSetting("BackupPath",BackupPath);DatabaseService.SaveSetting("AmountCaseFormat",CaseFormat);DatabaseService.SaveSetting("AmountCurrencyWording",CurrencyWording);DatabaseService.SaveSetting("AmountBaisaWording",BaisaWording);DatabaseService.SaveSetting("AmountIncludeBaisa",IncludeBaisa?"true":"false");DatabaseService.SaveSetting("AmountAddOnly",AddOnly?"true":"false");
            CompanyService.SetMasterSetting("Mailjet_ApiKey",(MailjetApiKey??"").Trim());CompanyService.SetMasterSetting("Mailjet_SecretKey",(MailjetSecretKey??"").Trim());CompanyService.SetMasterSetting("Mailjet_FromEmail",(MailjetFromEmail??"").Trim());CompanyService.SetMasterSetting("Mailjet_FromName",(MailjetFromName??"").Trim());
            PersistReminder();
            SyncService.Enabled=SyncEnabled;SyncService.ServerUrl=SyncServerUrl;
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Settings Changed","",$"Company={CompanyName}, Currency={Currency}, CaseFormat={CaseFormat}");
            StatusMessage="Settings saved successfully.";}
        void DoBackup(){try{var p=BackupService.CreateBackup();StatusMessage=$"Backup created: {System.IO.Path.GetFileName(p)}";}catch(Exception ex){StatusMessage=$"Backup failed: {ex.Message}";}}
        void DoRestore()
        {
            using var d=new System.Windows.Forms.OpenFileDialog{Filter="Database backup (*.db)|*.db",Title="Select a backup to restore"};
            if(System.IO.Directory.Exists(BackupPath)) d.InitialDirectory=BackupPath;
            if(d.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;
            if(System.Windows.MessageBox.Show("Restoring will OVERWRITE the current company's data with the selected backup.\n\nA safety copy of the current data is kept (.prerestore). Continue?","Confirm Restore",System.Windows.MessageBoxButton.YesNo,System.Windows.MessageBoxImage.Warning)!=System.Windows.MessageBoxResult.Yes)return;
            try{BackupService.RestoreBackup(d.FileName);StatusMessage="Backup restored. Please restart the application for the change to fully apply.";}
            catch(Exception ex){StatusMessage=$"Restore failed: {ex.Message}";}
        }
        void BrowsePdf(){using var d=new System.Windows.Forms.FolderBrowserDialog();if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK)PdfPath=d.SelectedPath;}
        void BrowseBackup(){using var d=new System.Windows.Forms.FolderBrowserDialog();if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK)BackupPath=d.SelectedPath;}
    }
}
