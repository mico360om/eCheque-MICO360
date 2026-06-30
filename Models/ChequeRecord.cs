using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace eCheque.MICO360.Models
{
    public class ChequeRecord : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void N([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        int _id; string _chequeNumber="",_payeeName="",_amountInWords="",_bankName="",_accountName="",_accountNumber="";
        int _profileId; string _profileName="",_currency="OMR",_remarks="",_reference="",_invoice="",_voucher="";
        string _preparedBy="",_approvedBy="",_department="",_category="",_status="Draft",_createdBy="",_pdfPath="",_cancelReason="";
        decimal _amount; DateTime _chequeDate=DateTime.Today,_createdDate=DateTime.Now; DateTime? _printedDate; int _printCount;

        public int Id{get=>_id;set{_id=value;N();}}
        public string ChequeNumber{get=>_chequeNumber;set{_chequeNumber=value;N();}}
        public DateTime ChequeDate{get=>_chequeDate;set{_chequeDate=value;N();}}
        public string PayeeName{get=>_payeeName;set{_payeeName=value;N();}}
        public decimal Amount{get=>_amount;set{_amount=value;N();}}
        public string AmountInWords{get=>_amountInWords;set{_amountInWords=value;N();}}
        public string BankName{get=>_bankName;set{_bankName=value;N();}}
        public string AccountName{get=>_accountName;set{_accountName=value;N();}}
        public string AccountNumber{get=>_accountNumber;set{_accountNumber=value;N();}}
        public int ProfileId{get=>_profileId;set{_profileId=value;N();}}
        public string ProfileName{get=>_profileName;set{_profileName=value;N();}}
        public string Currency{get=>_currency;set{_currency=value;N();}}
        public string Remarks{get=>_remarks;set{_remarks=value;N();}}
        public string ReferenceNumber{get=>_reference;set{_reference=value;N();}}
        public string InvoiceNumber{get=>_invoice;set{_invoice=value;N();}}
        public string VoucherNumber{get=>_voucher;set{_voucher=value;N();}}
        public string PreparedBy{get=>_preparedBy;set{_preparedBy=value;N();}}
        public string ApprovedBy{get=>_approvedBy;set{_approvedBy=value;N();}}
        public string Department{get=>_department;set{_department=value;N();}}
        public string PaymentCategory{get=>_category;set{_category=value;N();}}
        public string Status{get=>_status;set{_status=value;N();}}
        public string CreatedBy{get=>_createdBy;set{_createdBy=value;N();}}
        public DateTime CreatedDate{get=>_createdDate;set{_createdDate=value;N();}}
        public DateTime? PrintedDate{get=>_printedDate;set{_printedDate=value;N();}}
        public int PrintCount{get=>_printCount;set{_printCount=value;N();}}
        public string PdfFilePath{get=>_pdfPath;set{_pdfPath=value;N();}}
        public string CancellationReason{get=>_cancelReason;set{_cancelReason=value;N();}}
    }
}
