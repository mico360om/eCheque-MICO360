using System.IO;
using eCheque.MICO360.Models;
using Canvas = System.Windows.Controls.Canvas;
using TextBlock = System.Windows.Controls.TextBlock;
using Image = System.Windows.Controls.Image;
using TextWrapping = System.Windows.TextWrapping;
using TextAlignment = System.Windows.TextAlignment;
using FontWeights = System.Windows.FontWeights;
using FontFamily = System.Windows.Media.FontFamily;
using Brushes = System.Windows.Media.Brushes;
using BitmapImage = System.Windows.Media.Imaging.BitmapImage;
using BitmapCacheOption = System.Windows.Media.Imaging.BitmapCacheOption;

namespace eCheque.MICO360.Services
{
    /// <summary>
    /// Renders a cheque's PRINTABLE data (only the entered values, positioned per the saved layout) onto a
    /// WPF Canvas. The scanned cheque image is only shown on-screen (includeBackground = true) as an alignment
    /// guide — it is never included when printing.
    /// </summary>
    public static class ChequeRenderer
    {
        /// <summary>True when the profile has a visual layout (custom fields and/or a background template).</summary>
        public static bool HasLayout(ChequeProfile p) =>
            !string.IsNullOrWhiteSpace(p.FieldsJson) || !string.IsNullOrWhiteSpace(p.BackgroundImage);

        public static BitmapImage? DecodeImage(string base64)
        {
            if (string.IsNullOrWhiteSpace(base64)) return null;
            try
            {
                var bytes = Convert.FromBase64String(base64);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = new MemoryStream(bytes);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static Canvas Build(ChequeProfile p, ChequeRecord c, double pxPerMm, bool includeBackground)
        {
            var canvas = new Canvas
            {
                Width = Math.Max(1, p.ChequeWidth) * pxPerMm,
                Height = Math.Max(1, p.ChequeHeight) * pxPerMm,
                Background = Brushes.White,
                ClipToBounds = true
            };

            if (includeBackground)
            {
                var img = DecodeImage(p.BackgroundImage);
                if (img != null)
                {
                    var bg = new Image { Source = img, Width = canvas.Width, Height = canvas.Height, Stretch = System.Windows.Media.Stretch.Fill };
                    Canvas.SetLeft(bg, 0); Canvas.SetTop(bg, 0);
                    canvas.Children.Add(bg);
                }
            }

            var dateFmt = SafeSetting("DateFormat", "dd/MM/yyyy");
            foreach (var f in ChequeLayout.Parse(p))
            {
                if (!f.Enabled) continue;
                var val = ChequeLayout.ValueFor(f, c, dateFmt);
                if (string.IsNullOrWhiteSpace(val)) continue;

                var tb = new TextBlock
                {
                    Text = val,
                    FontFamily = new FontFamily(string.IsNullOrWhiteSpace(f.FontFamily) ? "Arial" : f.FontFamily),
                    FontSize = f.FontSize <= 0 ? 11 : f.FontSize,
                    FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = Brushes.Black,
                    Width = Math.Max(10, f.Width) * pxPerMm,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = f.Align switch { "Center" => TextAlignment.Center, "Right" => TextAlignment.Right, _ => TextAlignment.Left }
                };
                Canvas.SetLeft(tb, (f.X + p.PrintOffsetX) * pxPerMm);
                Canvas.SetTop(tb, (f.Y + p.PrintOffsetY) * pxPerMm);
                canvas.Children.Add(tb);
            }
            return canvas;
        }

        static string SafeSetting(string key, string def)
        {
            try { return DatabaseService.GetSetting(key, def); } catch { return def; }
        }
    }
}
