using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using eCheque.MICO360.ViewModels;
using FlowDocument = System.Windows.Documents.FlowDocument;
using Paragraph = System.Windows.Documents.Paragraph;
using Run = System.Windows.Documents.Run;
using PrintDialog = System.Windows.Controls.PrintDialog;
using FontFamily = System.Windows.Media.FontFamily;
using Thickness = System.Windows.Thickness;

namespace eCheque.MICO360.Views
{
    public partial class AboutView : UserControl
    {
        public AboutView() => InitializeComponent();

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not AboutViewModel vm) return;
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            var doc = new FlowDocument
            {
                PagePadding = new Thickness(48),
                FontFamily  = new FontFamily("Segoe UI"),
                FontSize    = 12,
                ColumnWidth = double.PositiveInfinity,
                PageWidth   = dlg.PrintableAreaWidth
            };
            doc.Blocks.Add(new Paragraph(new Run(vm.AppName)) { FontSize = 20, FontWeight = FontWeights.Bold });
            doc.Blocks.Add(new Paragraph(new Run(vm.Version)) { FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray });
            doc.Blocks.Add(new Paragraph(new Run(vm.Intro)) { Margin = new Thickness(0, 8, 0, 8) });
            doc.Blocks.Add(new Paragraph(new Run($"Company: {vm.CompanyName}")));
            doc.Blocks.Add(new Paragraph(new Run($"Contact Email: {vm.ContactEmail}")));
            doc.Blocks.Add(new Paragraph(new Run($"Website: {vm.Website}")));
            doc.Blocks.Add(new Paragraph(new Run($"App Version: {vm.Version}")));

            dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "About " + vm.AppName);
        }
    }
}
