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
        /// Preset a CUSTOM page size equal to the cheque's own dimensions BEFORE the dialog opens, so the
        /// dialog attempts to show the cheque size selected. WPF re-reads the printer default when the
        /// dialog is shown, so this is only a hint — SelectChequeMedia() (called after) is what makes it stick.
        /// </summary>
        public static void ApplyChequeMedia(System.Windows.Controls.PrintDialog dlg, double widthMm, double heightMm)
        {
            try
            {
                dlg.PrintTicket ??= new PrintTicket();
                dlg.PrintTicket.PageMediaSize = ChequeMediaSize(widthMm, heightMm);
            }
            catch { /* driver may not accept a custom size — falls back to the dialog default */ }
        }

        static PageMediaSize ChequeMediaSize(double widthMm, double heightMm)
            => new PageMediaSize(widthMm / 25.4 * 96.0, heightMm / 25.4 * 96.0);

        /// <summary>The page size the printer actually accepted, in DIP (1/96") and in mm, plus whether it
        /// matched the requested cheque size. Wmm/Hmm are for display; Wdip/Hdip drive the print layout.</summary>
        public readonly record struct ResolvedPage(double Wdip, double Hdip, double Wmm, double Hmm, bool Matched);

        /// <summary>
        /// AFTER the user has chosen a printer: force the output media to the cheque's exact size and
        /// VALIDATE it against that printer's capabilities. If the printer already offers a size that
        /// matches the cheque (within ~2 mm) that named size is selected; otherwise a custom size is used.
        /// If the driver can't honour the size it substitutes one — that substituted size is returned with
        /// Matched=false so the caller can warn. Never throws.
        /// </summary>
        public static ResolvedPage SelectChequeMedia(System.Windows.Controls.PrintDialog dlg, double widthMm, double heightMm)
        {
            double reqWdip = widthMm / 25.4 * 96.0, reqHdip = heightMm / 25.4 * 96.0;
            ResolvedPage Requested() => new(reqWdip, reqHdip, widthMm, heightMm, true);
            try
            {
                var queue = dlg.PrintQueue;
                if (queue == null) return Requested();

                double tol = 2.0 / 25.4 * 96.0; // ~2 mm tolerance

                // Prefer a driver-supported size that matches the cheque; else a custom size.
                PageMediaSize target = ChequeMediaSize(widthMm, heightMm);
                try
                {
                    foreach (var m in queue.GetPrintCapabilities().PageMediaSizeCapability)
                    {
                        if (m.Width.HasValue && m.Height.HasValue &&
                            Math.Abs(m.Width.Value  - reqWdip) <= tol &&
                            Math.Abs(m.Height.Value - reqHdip) <= tol)
                        { target = m; break; }
                    }
                }
                catch { /* no capabilities — keep the custom size */ }

                // Do NOT write queue.UserPrintTicket — that would mutate the user's saved printer default.
                var basis = dlg.PrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();
                var delta = new PrintTicket { PageMediaSize = target };
                var validated = queue.MergeAndValidatePrintTicket(basis, delta).ValidatedPrintTicket;
                dlg.PrintTicket = validated;

                var vs = validated.PageMediaSize;
                if (vs != null && vs.Width.HasValue && vs.Height.HasValue)
                {
                    double wmm = vs.Width.Value / 96.0 * 25.4, hmm = vs.Height.Value / 96.0 * 25.4;
                    bool matched = Math.Abs(wmm - widthMm) <= 2.0 && Math.Abs(hmm - heightMm) <= 2.0;
                    return new ResolvedPage(vs.Width.Value, vs.Height.Value, wmm, hmm, matched);
                }
                return Requested();
            }
            catch { return Requested(); } // never block printing on a driver quirk
        }

        // ─────────────────────── remembered printer (per-PC) ───────────────────────

        /// <summary>Settings key for this PC's remembered cheque printer. The Local_ prefix keeps it
        /// device-local — the sync registry excludes Local_* AppSettings from ever leaving this machine.</summary>
        public const string LocalPrinterKey = "Local_ChequePrinter";

        /// <summary>Finds an installed print queue by full name (local printers and network connections).
        /// Returns null when the printer is gone (unplugged/renamed) — callers fall back to the dialog.</summary>
        public static PrintQueue? FindQueue(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return null;
            try
            {
                var server = new PrintServer();
                foreach (var q in server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections }))
                    if (string.Equals(q.FullName, fullName, StringComparison.OrdinalIgnoreCase)) return q;
            }
            catch { }
            return null;
        }

        /// <summary>Resolves what page size a specific printer would actually use for the cheque, WITHOUT
        /// showing a dialog — used to show the ✓/⚠ paper check before the user prints.</summary>
        public static ResolvedPage ResolveForQueue(PrintQueue queue, double widthMm, double heightMm)
        {
            var dlg = new System.Windows.Controls.PrintDialog();
            try { dlg.PrintQueue = queue; } catch { }
            return SelectChequeMedia(dlg, widthMm, heightMm);
        }

        // ─────────────────────── paper-feed position (per-PC) ───────────────────────

        /// <summary>Where the cheque leaf physically sits in the tray when the driver substitutes a larger
        /// page (e.g. A4): Left, Center or Right. Device-local — trays differ per printer/PC.</summary>
        public const string LocalFeedAlignKey = "Local_FeedAlign";

        /// <summary>Horizontal offset (DIP) that moves 1:1 content to where the leaf actually sits on a
        /// substituted larger page. Left→0, Center→(page−content)/2, Right→page−content; 0 when the page
        /// isn't wider than the content. Pure and testable.</summary>
        public static double AnchorOffsetDip(string? align, double pageWdip, double contentWdip)
        {
            double slack = pageWdip - contentWdip;
            if (slack <= 0) return 0;
            return (align ?? "").Trim().ToLowerInvariant() switch
            {
                "center" or "centre" => slack / 2.0,
                "right" => slack,
                _ => 0, // Left (default): leaf against the left guide = page origin
            };
        }

        static string FeedAlignSetting()
        {
            try { return Services.DatabaseService.GetSetting(LocalFeedAlignKey, "Left"); }
            catch { return "Left"; } // printing before a DB is open (never in practice) — assume Left
        }

        /// <summary>
        /// Prints the content at real size (1:1); if the content is larger than the page it is scaled down
        /// uniformly to fit (never scaled up). The page size is passed in explicitly (from the validated print
        /// ticket) — dlg.PrintableArea* is NOT read, because WPF does not refresh it after a PrintTicket is
        /// reassigned post-ShowDialog. When the driver substituted a WIDER page than the cheque (e.g. A4 for a
        /// 190×85 leaf), the content is shifted horizontally per this PC's paper-feed setting (Left/Center/
        /// Right) so it lands where the leaf physically sits in the tray.
        /// </summary>
        public static void PrintActualSize(System.Windows.Controls.PrintDialog dlg, FrameworkElement content,
            double w, double h, double pageWdip, double pageHdip, string description)
        {
            content.Measure(new Size(w, h));
            content.Arrange(new Rect(0, 0, w, h));
            content.UpdateLayout();

            double aw = pageWdip, ah = pageHdip;
            if (aw <= 1 || ah <= 1) { aw = w; ah = h; } // safety: never arrange into a zero area

            if (w <= aw + 0.5 && h <= ah + 0.5)
            {
                double offX = AnchorOffsetDip(FeedAlignSetting(), aw, w);
                if (offX > 0.5)
                {
                    // Wrap in a page-sized canvas with the content moved to the leaf's real position.
                    var page = new System.Windows.Controls.Canvas { Width = aw, Height = ah };
                    System.Windows.Controls.Canvas.SetLeft(content, offX);
                    System.Windows.Controls.Canvas.SetTop(content, 0);
                    page.Children.Add(content);
                    page.Measure(new Size(aw, ah));
                    page.Arrange(new Rect(0, 0, aw, ah));
                    page.UpdateLayout();
                    dlg.PrintVisual(page, description);
                    return;
                }
                dlg.PrintVisual(content, description); // true 1:1, anchored top-left
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
