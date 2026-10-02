using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AriaBoost.UI
{
    /// <summary>
    /// CI için: her sayfanın ekran görüntüsünü PNG olarak kaydeder (--screenshot klasör).
    /// Hiçbir ayarı değiştirmez; yalnızca sayfaları açıp çizer.
    /// </summary>
    internal static class Screenshots
    {
        public static async Task CaptureAllAsync(MainWindow window, string dir)
        {
            Directory.CreateDirectory(dir);
            foreach (var page in MainWindow.PageIds)
            {
                window.Navigate(page);
                await Task.Delay(3000); // sayfaların arka planda veri yüklemesi için
                Save((FrameworkElement)window.Content, Path.Combine(dir, page + ".png"));
            }
        }

        private static void Save(FrameworkElement element, string path)
        {
            element.UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(element);
            int w = (int)(element.ActualWidth * dpi.DpiScaleX), h = (int)(element.ActualHeight * dpi.DpiScaleY);
            var bmp = new RenderTargetBitmap(w, h, dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            bmp.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(path)) encoder.Save(fs);
        }
    }
}
