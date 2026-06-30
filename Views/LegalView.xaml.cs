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
    public partial class LegalView : UserControl
    {
        public LegalView() => InitializeComponent();

        // Prints the legal text. Choosing "Microsoft Print to PDF" in the dialog exports a PDF.
        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not LegalViewModel vm) return;
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

            var title = new Paragraph(new Run(vm.Title)) { FontSize = 20, FontWeight = FontWeights.Bold };
            doc.Blocks.Add(title);
            doc.Blocks.Add(new Paragraph(new Run($"Last updated: {vm.LastUpdated}")) { FontSize = 11, Foreground = System.Windows.Media.Brushes.Gray });
            foreach (var para in (vm.Content ?? "").Replace("\r\n", "\n").Split("\n\n"))
                doc.Blocks.Add(new Paragraph(new Run(para.Trim())) { Margin = new Thickness(0, 0, 0, 8) });

            dlg.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, vm.Title);
        }
    }
}
