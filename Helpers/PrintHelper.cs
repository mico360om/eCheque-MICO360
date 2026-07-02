using System.Printing;
using Size = System.Windows.Size;
using Rect = System.Windows.Rect;
using FrameworkElement = System.Windows.FrameworkElement;
using StretchDirection = System.Windows.Controls.StretchDirection;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace eCheque.MICO360.Helpers
{
    /// <summary>
    /// Shared printing helpers so every print path selects the correct page size and prints the
    /// cheque at true 1:1 scale (only shrinking if the cheque is larger than the printable page),
    /// which is what keeps the printed text aligned with a real pre-printed cheque.
    /// </summary>
    public static class PrintHelper
    {
        public static PageMediaSize MediaFor(string? paper) => new PageMediaSize(paper switch
        {
            "Letter" => PageMediaSizeName.NorthAmericaLetter,
            "Legal"  => PageMediaSizeName.NorthAmericaLegal,
            "A5"     => PageMediaSizeName.ISOA5,
            _         => PageMediaSizeName.ISOA4,
        });

        /// <summary>Page size in millimetres for the PDF, matching the profile's paper choice.</summary>
        public static (double W, double H) PageSizeMm(string? paper) => paper switch
        {
            "Letter" => (215.9, 279.4),
            "Legal"  => (215.9, 355.6),
            "A5"     => (148.0, 210.0),
            _         => (210.0, 297.0),
        };

        /// <summary>Preset the chosen paper size on the dialog so it's selected when the user prints.</summary>
        public static void ApplyMediaSize(System.Windows.Controls.PrintDialog dlg, string? paper)
        {
            try
            {
                dlg.PrintTicket ??= new PrintTicket();
                dlg.PrintTicket.PageMediaSize = MediaFor(paper);
            }
            catch { /* some drivers reject a preset size — the dialog default is then used */ }
        }

        /// <summary>
        /// Preset a CUSTOM page size equal to the cheque's own dimensions so the cheque prints on a
        /// cheque-sized page (not a default A4 sheet). This is what keeps a real cheque aligned.
        /// </summary>
        public static void ApplyChequeMedia(System.Windows.Controls.PrintDialog dlg, double widthMm, double heightMm)
        {
            try
            {
                double wDip = widthMm  / 25.4 * 96.0;
                double hDip = heightMm / 25.4 * 96.0;
                dlg.PrintTicket ??= new PrintTicket();
                dlg.PrintTicket.PageMediaSize = new PageMediaSize(wDip, hDip);
            }
            catch { /* driver may not accept a custom size — falls back to the dialog default */ }
        }

        /// <summary>
        /// Prints the content at real size at the top-left of the page; if the content is bigger than
        /// the printable area it is scaled down uniformly to fit (never scaled up).
        /// </summary>
        public static void PrintActualSize(System.Windows.Controls.PrintDialog dlg, FrameworkElement content, double w, double h, string description)
        {
            content.Measure(new Size(w, h));
            content.Arrange(new Rect(0, 0, w, h));
            content.UpdateLayout();

            double aw = dlg.PrintableAreaWidth, ah = dlg.PrintableAreaHeight;
            if (w <= aw && h <= ah)
            {
                dlg.PrintVisual(content, description); // 1:1, anchored top-left
                return;
            }

            var vb = new System.Windows.Controls.Viewbox
            {
                Stretch = System.Windows.Media.Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                Child = content,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            vb.Measure(new Size(aw, ah));
            vb.Arrange(new Rect(0, 0, aw, ah));
            vb.UpdateLayout();
            dlg.PrintVisual(vb, description);
        }
    }
}
