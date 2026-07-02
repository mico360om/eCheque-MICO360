using eCheque.MICO360.Models;
using Canvas = System.Windows.Controls.Canvas;
using TextBlock = System.Windows.Controls.TextBlock;
using Rectangle = System.Windows.Shapes.Rectangle;
using Line = System.Windows.Shapes.Line;
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using FontFamily = System.Windows.Media.FontFamily;
using FontWeights = System.Windows.FontWeights;
using TextWrapping = System.Windows.TextWrapping;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Builds the printable cheque canvas for a cheque+profile. Shared by the single-cheque Print Preview
    /// and the bulk-print flow so both render identically. Uses the visual layout when the profile has one,
    /// otherwise the classic decorative fallback.
    /// </summary>
    public static class ChequePrintBuilder
    {
        public const double PxPerMm = 96.0 / 25.4;

        public static Canvas Build(ChequeRecord cheque, ChequeProfile profile, double w, double h, bool includeBackground = false)
        {
            if (ChequeRenderer.HasLayout(profile))
                return ChequeRenderer.Build(profile, cheque, PxPerMm, includeBackground);

            var canvas = new Canvas { Width = w, Height = h, Background = Brushes.White, ClipToBounds = true };

            var header = new Rectangle { Width = w, Height = 9 * PxPerMm, Fill = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0)) };
            Canvas.SetLeft(header, 0); Canvas.SetTop(header, 0);
            canvas.Children.Add(header);

            canvas.Children.Add(MakeTb(profile.BankName + "    A/C: " + cheque.AccountNumber,
                profile.FontFamily, 8, true, Colors.Black, 5, 1.5));

            var border = new Rectangle { Width = w - 1, Height = h - 1,
                Stroke = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), StrokeThickness = 1, Fill = Brushes.Transparent };
            Canvas.SetLeft(border, 0.5); Canvas.SetTop(border, 0.5);
            canvas.Children.Add(border);

            var micr = new Rectangle { Width = w, Height = 12 * PxPerMm, Fill = new SolidColorBrush(Color.FromRgb(0xF8, 0xF0, 0xF0)) };
            Canvas.SetLeft(micr, 0); Canvas.SetTop(micr, h - 12 * PxPerMm);
            canvas.Children.Add(micr);

            var sig = new Line { X1 = w - 52 * PxPerMm, Y1 = h - 12 * PxPerMm, X2 = w - 4 * PxPerMm, Y2 = h - 12 * PxPerMm,
                Stroke = Brushes.Gray, StrokeThickness = 0.8 };
            canvas.Children.Add(sig);
            canvas.Children.Add(MakeTb("Authorized Signature", profile.FontFamily, 7, false,
                Color.FromRgb(0x88, 0x88, 0x88), w - 52 * PxPerMm, h - 9.5 * PxPerMm));

            void AddField(string label, string value, double xMm, double yMm)
            {
                canvas.Children.Add(MakeTb(label, profile.FontFamily, 7, false,
                    Color.FromRgb(0x88, 0x88, 0x88), xMm * PxPerMm, (yMm - 4) * PxPerMm));
                canvas.Children.Add(MakeTb(value, profile.FontFamily, profile.FontSize,
                    profile.IsBold, Colors.Black, xMm * PxPerMm, yMm * PxPerMm));
                var ul = new Line { X1 = xMm * PxPerMm, Y1 = (yMm + 5) * PxPerMm,
                    X2 = System.Math.Min((xMm + 80) * PxPerMm, w - 5 * PxPerMm), Y2 = (yMm + 5) * PxPerMm,
                    Stroke = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD)), StrokeThickness = 0.5 };
                canvas.Children.Add(ul);
            }

            AddField("Date:", cheque.ChequeDate.ToString("dd / MM / yyyy"), profile.DateX, profile.DateY);
            AddField("Pay to:", cheque.PayeeName, profile.PayeeX, profile.PayeeY);
            AddField("Amount:", $"{cheque.Currency} {cheque.Amount:N3}", profile.AmountNumX, profile.AmountNumY);
            AddField("In words:", cheque.AmountInWords, profile.AmountWordsX, profile.AmountWordsY);
            AddField("Cheque No:", cheque.ChequeNumber, profile.ChequeNumX, profile.ChequeNumY);

            return canvas;
        }

        static TextBlock MakeTb(string text, string font, double size, bool bold, Color color, double x, double y)
        {
            var tb = new TextBlock
            {
                Text = text, FontFamily = new FontFamily(font), FontSize = size,
                FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
                Foreground = new SolidColorBrush(color), TextWrapping = TextWrapping.Wrap, MaxWidth = 200
            };
            Canvas.SetLeft(tb, x); Canvas.SetTop(tb, y);
            return tb;
        }
    }
}
