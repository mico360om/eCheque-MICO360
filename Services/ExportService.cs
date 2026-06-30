using OfficeOpenXml;
using OfficeOpenXml.Style;
using eCheque.MICO360.Models;
using System.Drawing;
using System.IO;

namespace eCheque.MICO360.Services
{
    public static class ExportService
    {
        static ExportService(){ExcelPackage.LicenseContext=LicenseContext.NonCommercial;}

        public static string ExportCheques(List<ChequeRecord> cheques, string title="Cheque Register")
        {
            var path=Path.Combine(DatabaseService.GetSetting("PdfSavePath",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"eCheque MICO360","Reports")),$"ChequeReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var pkg=new ExcelPackage();
            var ws=pkg.Workbook.Worksheets.Add("Cheques");
            ws.Cells["A1"].Value="eCheque MICO360";ws.Cells["A1"].Style.Font.Bold=true;ws.Cells["A1"].Style.Font.Size=14;
            ws.Cells["A2"].Value=title;ws.Cells["A2"].Style.Font.Bold=true;
            ws.Cells["A3"].Value=$"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}";
            var hdrs=new[]{"#","Cheque No","Date","Payee Name","Bank","Currency","Amount","Amount in Words","Status","Prepared By","Print Count","Remarks"};
            for(int i=0;i<hdrs.Length;i++){var cell=ws.Cells[5,i+1];cell.Value=hdrs[i];cell.Style.Font.Bold=true;cell.Style.Fill.PatternType=ExcelFillStyle.Solid;cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(139,24,24));cell.Style.Font.Color.SetColor(Color.White);}
            for(int i=0;i<cheques.Count;i++){var c=cheques[i];var row=i+6;ws.Cells[row,1].Value=i+1;ws.Cells[row,2].Value=c.ChequeNumber;ws.Cells[row,3].Value=c.ChequeDate.ToString("dd/MM/yyyy");ws.Cells[row,4].Value=c.PayeeName;ws.Cells[row,5].Value=c.BankName;ws.Cells[row,6].Value=c.Currency;ws.Cells[row,7].Value=(double)c.Amount;ws.Cells[row,7].Style.Numberformat.Format="#,##0.000";ws.Cells[row,8].Value=c.AmountInWords;ws.Cells[row,9].Value=c.Status;ws.Cells[row,10].Value=c.PreparedBy;ws.Cells[row,11].Value=c.PrintCount;ws.Cells[row,12].Value=c.Remarks;if(i%2==1){ws.Cells[row,1,row,hdrs.Length].Style.Fill.PatternType=ExcelFillStyle.Solid;ws.Cells[row,1,row,hdrs.Length].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248,235,235));}}
            ws.Cells.AutoFitColumns();
            pkg.SaveAs(new FileInfo(path));
            return path;
        }
    }
}
