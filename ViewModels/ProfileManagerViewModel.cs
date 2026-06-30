using System.Collections.ObjectModel;
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
        public ChequeProfile? Selected{get=>_sel;set{Set(ref _sel,value);if(value!=null)Edit=Clone(value);}}
        public ChequeProfile Edit{get=>_edit;set=>Set(ref _edit,value);}
        public bool IsEditing{get=>_isEditing;set=>Set(ref _isEditing,value);}
        public string StatusMessage{get=>_status;set=>Set(ref _status,value);}
        public List<string> FontFamilies{get;}=new(){"Arial","Times New Roman","Calibri","Courier New","Verdana","Tahoma"};
        public List<string> PaperSizes{get;}=new(){"A4","Letter","Legal","A5"};
        public ICommand NewCommand{get;}
        public ICommand SaveCommand{get;}
        public ICommand DeleteCommand{get;}
        public ICommand DuplicateCommand{get;}
        public ICommand CancelEditCommand{get;}
        public ICommand DesignLayoutCommand{get;}
        public ProfileManagerViewModel(){NewCommand=new RelayCommand(NewProfile);SaveCommand=new RelayCommand(SaveProfile,()=>IsEditing);DeleteCommand=new RelayCommand(DeleteProfile,()=>Selected!=null);DuplicateCommand=new RelayCommand(Duplicate,()=>Selected!=null);CancelEditCommand=new RelayCommand(()=>IsEditing=false);DesignLayoutCommand=new RelayCommand(OpenDesigner,()=>Selected!=null);}
        void OpenDesigner()
        {
            if(Selected==null)return;
            var w=new Views.ChequeLayoutDesigner(Selected);
            w.ShowDialog();
            if(w.Saved){Load();StatusMessage="Layout saved.";}
        }
        public void Load(){Profiles=new ObservableCollection<ChequeProfile>(ChequeService.GetProfiles(false));}
        void NewProfile(){Edit=new ChequeProfile{CreatedBy=AuthService.CurrentUser?.Username??"",AccountName=DatabaseService.GetSetting("CompanyName","")};IsEditing=true;}
        void SaveProfile(){if(string.IsNullOrWhiteSpace(Edit.Name)){StatusMessage="Profile name is required.";return;}ChequeService.SaveProfile(Edit);Load();Selected=Profiles.FirstOrDefault(p=>p.Id==Edit.Id);IsEditing=false;StatusMessage="Profile saved.";}
        void DeleteProfile()
        {
            if(Selected==null)return;
            int used=ChequeService.CountChequesUsingProfile(Selected.Id);
            if(used>0){StatusMessage=$"Cannot delete — this profile is used by {used} cheque(s).";return;}
            if(System.Windows.MessageBox.Show($"Delete profile '{Selected.Name}'?","Confirm Delete",System.Windows.MessageBoxButton.YesNo,System.Windows.MessageBoxImage.Warning)!=System.Windows.MessageBoxResult.Yes)return;
            if(ChequeService.DeleteProfile(Selected.Id)){Load();StatusMessage="Profile deleted.";}
            else StatusMessage="Profile is in use and cannot be deleted.";
        }
        void Duplicate(){if(Selected==null)return;var c=Clone(Selected);c.Id=0;c.Name+=" (Copy)";c.CreatedBy=AuthService.CurrentUser?.Username??"";Edit=c;IsEditing=true;}
        ChequeProfile Clone(ChequeProfile p)=>new(){Id=p.Id,Name=p.Name,BankName=p.BankName,AccountName=p.AccountName,AccountNumber=p.AccountNumber,ChequeWidth=p.ChequeWidth,ChequeHeight=p.ChequeHeight,DateX=p.DateX,DateY=p.DateY,PayeeX=p.PayeeX,PayeeY=p.PayeeY,AmountNumX=p.AmountNumX,AmountNumY=p.AmountNumY,AmountWordsX=p.AmountWordsX,AmountWordsY=p.AmountWordsY,ChequeNumX=p.ChequeNumX,ChequeNumY=p.ChequeNumY,FontFamily=p.FontFamily,FontSize=p.FontSize,IsBold=p.IsBold,PrintOffsetX=p.PrintOffsetX,PrintOffsetY=p.PrintOffsetY,PaperSize=p.PaperSize,IsActive=p.IsActive,CreatedDate=p.CreatedDate,CreatedBy=p.CreatedBy};
    }
}
