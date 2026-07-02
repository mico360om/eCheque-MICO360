using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using eCheque.MICO360.Helpers;
using eCheque.MICO360.Models;
using eCheque.MICO360.Services;
namespace eCheque.MICO360.ViewModels
{
    public class ProfileManagerViewModel : BaseViewModel
    {
        ObservableCollection<ChequeProfile> _profiles=new(); ChequeProfile? _sel; ChequeProfile _edit=new(); bool _isEditing; string _status="";
        public ObservableCollection<ChequeProfile> Profiles{get=>_profiles;set=>Set(ref _profiles,value);}
        public ChequeProfile? Selected{get=>_sel;set{Set(ref _sel,value);if(value!=null)Edit=Clone(value);RefreshTemplate();}}
        public ChequeProfile Edit{get=>_edit;set{Set(ref _edit,value);RefreshTemplate();}}
        public bool IsEditing{get=>_isEditing;set=>Set(ref _isEditing,value);}
        public string StatusMessage{get=>_status;set=>Set(ref _status,value);}
        public List<string> FontFamilies{get;}=new(){"Arial","Times New Roman","Calibri","Courier New","Verdana","Tahoma"};
        public List<string> PaperSizes{get;}=new(){"A4","Letter","Legal","A5"};

        /// <summary>On-screen preview of the uploaded scanned cheque (never printed).</summary>
        public System.Windows.Media.Imaging.BitmapSource? TemplatePreview => ChequeRenderer.DecodeImage(Edit?.BackgroundImage ?? "");
        public bool HasTemplate => !string.IsNullOrWhiteSpace(Edit?.BackgroundImage);
        public string TemplateStatus => HasTemplate ? "✔ Scanned cheque uploaded — use Design Layout to position the fields." : "No scanned cheque yet. Upload one, then open Design Layout to place the fields.";

        public ICommand NewCommand{get;}
        public ICommand SaveCommand{get;}
        public ICommand DeleteCommand{get;}
        public ICommand DuplicateCommand{get;}
        public ICommand CancelEditCommand{get;}
        public ICommand DesignLayoutCommand{get;}
        public ICommand UploadImageCommand{get;}
        public ICommand RemoveImageCommand{get;}
        public ProfileManagerViewModel()
        {
            NewCommand=new RelayCommand(NewProfile);
            SaveCommand=new RelayCommand(SaveProfile,()=>IsEditing);
            DeleteCommand=new RelayCommand(DeleteProfile,()=>Selected!=null);
            DuplicateCommand=new RelayCommand(Duplicate,()=>Selected!=null);
            CancelEditCommand=new RelayCommand(()=>{IsEditing=false;if(Selected!=null)Edit=Clone(Selected);});
            DesignLayoutCommand=new RelayCommand(OpenDesigner,()=>Selected!=null);
            UploadImageCommand=new RelayCommand(UploadImage,()=>IsEditing||Selected!=null);
            RemoveImageCommand=new RelayCommand(RemoveImage,()=>HasTemplate);
        }

        void RefreshTemplate(){OnPropertyChanged(nameof(TemplatePreview));OnPropertyChanged(nameof(HasTemplate));OnPropertyChanged(nameof(TemplateStatus));System.Windows.Input.CommandManager.InvalidateRequerySuggested();}

        void UploadImage()
        {
            var dlg=new Microsoft.Win32.OpenFileDialog
            {
                Title="Select the scanned cheque image",
                Filter="Image files (*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff)|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|All files (*.*)|*.*"
            };
            if(dlg.ShowDialog()!=true) return;
            try
            {
                var info=new FileInfo(dlg.FileName);
                if(info.Length>8*1024*1024){StatusMessage="Image is too large (max 8 MB). Please use a smaller scan.";return;}
                Edit.BackgroundImage=Convert.ToBase64String(File.ReadAllBytes(dlg.FileName));
                if(!IsEditing) IsEditing=true; // let the user Save
                RefreshTemplate();
                StatusMessage="Scanned cheque loaded. Click Save Profile, then Design Layout to position fields.";
            }
            catch(Exception ex)
            {
                BugReportService.Report(ex,"UploadImage");
                StatusMessage="Could not read that image: "+ex.Message;
            }
        }

        void RemoveImage()
        {
            if(!HasTemplate) return;
            Edit.BackgroundImage="";
            if(!IsEditing) IsEditing=true;
            RefreshTemplate();
            StatusMessage="Scanned cheque removed.";
        }

        void OpenDesigner()
        {
            if(Selected==null){StatusMessage="Select a profile first.";return;}
            try
            {
                var w=new Views.ChequeLayoutDesigner(Selected);
                w.ShowDialog();
                if(w.Saved){Load();StatusMessage="Layout saved.";}
            }
            catch(Exception ex)
            {
                BugReportService.Report(ex,"OpenDesigner");
                System.Windows.MessageBox.Show($"Could not open the layout designer:\n\n{ex.Message}","Design Layout",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Warning);
            }
        }
        public void Load(){var keepId=Selected?.Id??Edit?.Id??0;Profiles=new ObservableCollection<ChequeProfile>(ChequeService.GetProfiles(false));if(keepId>0)Selected=Profiles.FirstOrDefault(p=>p.Id==keepId);}
        void NewProfile(){Edit=new ChequeProfile{CreatedBy=AuthService.CurrentUser?.Username??"",AccountName=DatabaseService.GetSetting("CompanyName","")};IsEditing=true;StatusMessage="";}
        void SaveProfile(){if(string.IsNullOrWhiteSpace(Edit.Name)){StatusMessage="Profile name is required.";ToastService.Error("Profile name is required.");return;}ChequeService.SaveProfile(Edit);Load();Selected=Profiles.FirstOrDefault(p=>p.Id==Edit.Id);IsEditing=false;StatusMessage="Profile saved.";ToastService.Success($"Profile '{Edit.Name}' saved.");}
        void DeleteProfile()
        {
            if(Selected==null)return;
            int used=ChequeService.CountChequesUsingProfile(Selected.Id);
            if(used>0){StatusMessage=$"Cannot delete — this profile is used by {used} cheque(s).";return;}
            if(System.Windows.MessageBox.Show($"Delete profile '{Selected.Name}'?","Confirm Delete",System.Windows.MessageBoxButton.YesNo,System.Windows.MessageBoxImage.Warning)!=System.Windows.MessageBoxResult.Yes)return;
            if(ChequeService.DeleteProfile(Selected.Id)){Load();StatusMessage="Profile deleted.";}
            else StatusMessage="Profile is in use and cannot be deleted.";
        }
        void Duplicate(){if(Selected==null)return;var c=Clone(Selected);c.Id=0;c.Name+=" (Copy)";c.CreatedBy=AuthService.CurrentUser?.Username??"";Edit=c;IsEditing=true;StatusMessage="Duplicated — the scanned cheque and field layout were copied. Click Save Profile.";}
        ChequeProfile Clone(ChequeProfile p)=>new(){Id=p.Id,Name=p.Name,BankName=p.BankName,AccountName=p.AccountName,AccountNumber=p.AccountNumber,ChequeWidth=p.ChequeWidth,ChequeHeight=p.ChequeHeight,DateX=p.DateX,DateY=p.DateY,PayeeX=p.PayeeX,PayeeY=p.PayeeY,AmountNumX=p.AmountNumX,AmountNumY=p.AmountNumY,AmountWordsX=p.AmountWordsX,AmountWordsY=p.AmountWordsY,ChequeNumX=p.ChequeNumX,ChequeNumY=p.ChequeNumY,FontFamily=p.FontFamily,FontSize=p.FontSize,IsBold=p.IsBold,PrintOffsetX=p.PrintOffsetX,PrintOffsetY=p.PrintOffsetY,PaperSize=p.PaperSize,IsActive=p.IsActive,CreatedDate=p.CreatedDate,CreatedBy=p.CreatedBy,BackgroundImage=p.BackgroundImage,FieldsJson=p.FieldsJson};
    }
}
