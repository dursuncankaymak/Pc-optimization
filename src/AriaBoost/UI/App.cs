using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    internal static class App
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string screenshotDir = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--screenshot") screenshotDir = args[i + 1];
            }

            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            // Pencere, Dispatcher döngüsü başlamadan (app.Run'dan önce) kurulur; bu sırada başlayan
            // async işlemlerin devamı da arayüz iş parçacığına dönsün diye bağlam baştan kurulur.
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
            using (var s = typeof(App).Assembly.GetManifestResourceStream("AriaBoost.Theme.xaml"))
            {
                app.Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(s));
            }

            int exitCode = 0;
            app.DispatcherUnhandledException += (_, e) =>
            {
                LogError(e.Exception);
                e.Handled = true;
                if (screenshotDir != null)
                {
                    exitCode = 1;
                    app.Shutdown(1);
                }
                else
                {
                    MessageBox.Show("Beklenmeyen bir hata oluştu:\n\n" + e.Exception.Message, "Aria Boost", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            TaskScheduler.UnobservedTaskException += (_, e) => { LogError(e.Exception); e.SetObserved(); };

            var state = new AppState();
            // Önceki oturum çöktüyse durdurulmuş kalan Wi-Fi taramasını geri aç; kapanırken de aç.
            try { WifiScan.RestoreAll(state.Store); } catch (Exception ex) { LogError(ex); }
            app.Exit += (_, __) =>
            {
                try { WifiScan.RestoreAll(state.Store); } catch (Exception ex) { LogError(ex); }
            };

            var window = new MainWindow(state);
            if (screenshotDir != null)
            {
                window.Loaded += async (_, __) =>
                {
                    try
                    {
                        await Screenshots.CaptureAllAsync(window, screenshotDir);
                    }
                    catch (Exception ex)
                    {
                        LogError(ex);
                        exitCode = 1;
                    }
                    app.Shutdown(exitCode);
                };
            }
            app.Run(window);
            return exitCode;
        }

        public static void LogError(Exception ex)
        {
            try
            {
                Directory.CreateDirectory(BackupStore.DefaultDirectory);
                File.AppendAllText(Path.Combine(BackupStore.DefaultDirectory, "error.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\r\n\r\n");
            }
            catch { }
        }
    }
}
