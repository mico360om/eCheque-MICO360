using eCheque.MICO360.Models;
using Canvas = System.Windows.Controls.Canvas;
using TextBlock = System.Windows.Controls.TextBlock;
using Line = System.Windows.Shapes.Line;
using Rectangle = System.Windows.Shapes.Rectangle;
using Brushes = System.Windows.Media.Brushes;
using FontFamily = System.Windows.Media.FontFamily;
using FontWeights = System.Windows.FontWeights;
using Thickness = System.Windows.Thickness;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Builds a printable ALIGNMENT CALIBRATION sheet for a profile: the cheque outline, a millimetre ruler
    /// along the top and left edges, and a labelled crosshair at every field anchor (with the profile's current
    /// print offset applied). The user prints it on plain paper, lays it over a real cheque, reads how far each
    /// marker is off using the printed ruler, and corrects the Print Offset by that amount — no more guessing.
    /// </summary>
    public static class CalibrationRenderer
    {
        public static Canvas Build(ChequeProfile p, double pxPerMm)
        {
            double wmm = Math.Max(1, p.ChequeWidth), hmm = Math.Max(1, p.ChequeHeight);
            var canvas = new Canvas
            {
                Width = wmm * pxPerMm,
                Height = hmm * pxPerMm,
                Background = Brushes.White,
                ClipToBounds = true
            };

            // Cheque boundary.
            var border = new Rectangle
            {
                Width = canvas.Width, Height = canvas.Height,
                Stroke = Brushes.Black, StrokeThickness = 0.6
            };
            Canvas.SetLeft(border, 0); Canvas.SetTop(border, 0);
            canvas.Children.Add(border);

            // Millimetre rulers along the top and left edges (tick every 5 mm, labelled every 10 mm).
            for (int mm = 0; mm <= wmm; mm += 5)
            {
                bool major = mm % 10 == 0;
                double x = mm * pxPerMm, len = (major ? 4.0 : 2.0) * pxPerMm;
                canvas.Children.Add(VLine(x, 0, len));
                if (major && mm > 0) canvas.Children.Add(Label($"{mm}", x + 0.4 * pxPerMm, 0.2 * pxPerMm, 6.5));
            }
            for (int mm = 0; mm <= hmm; mm += 5)
            {
                bool major = mm % 10 == 0;
                double y = mm * pxPerMm, len = (major ? 4.0 : 2.0) * pxPerMm;
                canvas.Children.Add(HLine(0, y, len));
                if (major && mm > 0) canvas.Children.Add(Label($"{mm}", 0.3 * pxPerMm, y + 0.2 * pxPerMm, 6.5));
            }

            // A crosshair + label at each enabled field anchor, with the profile's current offset applied.
            foreach (var f in ChequeLayout.Parse(p))
            {
                if (!f.Enabled) continue;
                double cx = (f.X + p.PrintOffsetX) * pxPerMm, cy = (f.Y + p.PrintOffsetY) * pxPerMm;
                double arm = 3.0 * pxPerMm;
                canvas.Children.Add(HLine(cx - arm, cy, arm * 2));
                canvas.Children.Add(VLine(cx, cy - arm, arm * 2));
                canvas.Children.Add(Label($"{FieldName(f.Key)}  ({f.X:0}, {f.Y:0})", cx + 1.2 * pxPerMm, cy + 0.6 * pxPerMm, 7));
            }

            // Footer legend so the printed sheet is self-describing.
            string off = (p.PrintOffsetX == 0 && p.PrintOffsetY == 0) ? "no offset" : $"offset X={p.PrintOffsetX:0.#}, Y={p.PrintOffsetY:0.#} mm";
            canvas.Children.Add(Label(
                $"CALIBRATION — {p.Name}  •  {wmm:0}×{hmm:0} mm  •  {off}  •  ruler in mm from top-left",
                2 * pxPerMm, hmm * pxPerMm - 5.5 * pxPerMm, 7));
            return canvas;
        }

        static Line VLine(double x, double y, double len) => new()
        { X1 = x, Y1 = y, X2 = x, Y2 = y + len, Stroke = Brushes.Black, StrokeThickness = 0.5 };

        static Line HLine(double x, double y, double len) => new()
        { X1 = x, Y1 = y, X2 = x + len, Y2 = y, Stroke = Brushes.Black, StrokeThickness = 0.5 };

        static TextBlock Label(string text, double x, double y, double size)
        {
            var tb = new TextBlock
            {
                Text = text, FontFamily = new FontFamily("Arial"), FontSize = size,
                FontWeight = FontWeights.Normal, Foreground = Brushes.Black, Margin = new Thickness(0)
            };
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
            return tb;
        }

        static string FieldName(string key) => key switch
        {
            "Date" => "Date",
            "Payee" => "Payee",
            "AmountNum" => "Amount (fig)",
            "AmountWords" => "Amount (words)",
            "ChequeNumber" => "Cheque #",
            _ => key
        };
    }
}
