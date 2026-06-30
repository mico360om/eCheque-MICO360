using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        string _co="",_cur="OMR",_df="dd/MM/yyyy",_pdf="",_bak="",_cf="UPPERCASE",_status="";
        string _currencyWording="Omani Rials",_baisaWording="Baisa"; bool _includeBaisa=true,_addOnly=true;
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
        public ICommand BrowsePdfCommand{get;}
        public ICommand BrowseBackupCommand{get;}
        public SettingsViewModel(){SaveCommand=new RelayCommand(Save);BackupCommand=new RelayCommand(DoBackup);BrowsePdfCommand=new RelayCommand(BrowsePdf);BrowseBackupCommand=new RelayCommand(BrowseBackup);}
        public void Load(){CompanyName=DatabaseService.GetSetting("CompanyName","My Company LLC");Currency=DatabaseService.GetSetting("DefaultCurrency","OMR");DateFormat=DatabaseService.GetSetting("DateFormat","dd/MM/yyyy");PdfPath=DatabaseService.GetSetting("PdfSavePath","");BackupPath=DatabaseService.GetSetting("BackupPath","");CaseFormat=DatabaseService.GetSetting("AmountCaseFormat","UPPERCASE");CurrencyWording=DatabaseService.GetSetting("AmountCurrencyWording","Omani Rials");BaisaWording=DatabaseService.GetSetting("AmountBaisaWording","Baisa");IncludeBaisa=DatabaseService.GetSetting("AmountIncludeBaisa","true")=="true";AddOnly=DatabaseService.GetSetting("AmountAddOnly","true")=="true";}
        void Save(){
            if(!string.IsNullOrWhiteSpace(PdfPath)&&!System.IO.Directory.Exists(PdfPath)){StatusMessage=$"PDF output path does not exist: {PdfPath}";return;}
            if(!string.IsNullOrWhiteSpace(BackupPath)&&!System.IO.Directory.Exists(BackupPath)){StatusMessage=$"Backup path does not exist: {BackupPath}";return;}
            DatabaseService.SaveSetting("CompanyName",CompanyName);DatabaseService.SaveSetting("DefaultCurrency",Currency);DatabaseService.SaveSetting("DateFormat",DateFormat);DatabaseService.SaveSetting("PdfSavePath",PdfPath);DatabaseService.SaveSetting("BackupPath",BackupPath);DatabaseService.SaveSetting("AmountCaseFormat",CaseFormat);DatabaseService.SaveSetting("AmountCurrencyWording",CurrencyWording);DatabaseService.SaveSetting("AmountBaisaWording",BaisaWording);DatabaseService.SaveSetting("AmountIncludeBaisa",IncludeBaisa?"true":"false");DatabaseService.SaveSetting("AmountAddOnly",AddOnly?"true":"false");
            DatabaseService.LogAudit(AuthService.CurrentUser?.Username??"","Settings Changed","",$"Company={CompanyName}, Currency={Currency}, CaseFormat={CaseFormat}");
            StatusMessage="Settings saved successfully.";}
        void DoBackup(){try{var p=BackupService.CreateBackup();StatusMessage=$"Backup created: {System.IO.Path.GetFileName(p)}";}catch(Exception ex){StatusMessage=$"Backup failed: {ex.Message}";}}
        void BrowsePdf(){using var d=new System.Windows.Forms.FolderBrowserDialog();if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK)PdfPath=d.SelectedPath;}
        void BrowseBackup(){using var d=new System.Windows.Forms.FolderBrowserDialog();if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK)BackupPath=d.SelectedPath;}
    }
}
