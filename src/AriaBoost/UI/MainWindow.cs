using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    internal sealed class MainWindow : Window
    {
        public static readonly string[] PageIds = { "overview", "tweaks", "ping", "startup", "cleaner" };

        private readonly AppState state;
        private readonly Dictionary<string, RadioButton> navButtons = new Dictionary<string, RadioButton>();
        private readonly Dictionary<string, FrameworkElement> pages = new Dictionary<string, FrameworkElement>();
        private readonly ContentControl host = new ContentControl();
        private readonly Border restartBanner;
        private readonly Button revertAllButton;

        public MainWindow(AppState state)
        {
            this.state = state;
            Title = "Aria Boost";
            Width = 1180;
            Height = 780;
            MinWidth = 980;
            MinHeight = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Ui.Brush("Bg");
            FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
            Foreground = Ui.Brush("Text");
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            Icon = LoadIcon();

            var root = new Grid { Background = Ui.Brush("Bg") };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            root.ColumnDefinitions.Add(new ColumnDefinition());

            // ---- Kenar çubuğu ----
            var sidebar = new DockPanel { LastChildFill = false };
            var sidebarBorder = new Border
            {
                Background = Ui.Brush("Sidebar"),
                BorderBrush = Ui.Brush("Border"),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Padding = new Thickness(14, 22, 14, 18),
                Child = sidebar,
            };

            var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 0, 26) };
            DockPanel.SetDock(brand, Dock.Top);
            brand.Children.Add(new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(10),
                Background = Ui.Brush("AccentGradient"),
                Child = new TextBlock
                {
                    Text = Glyphs.Bolt,
                    FontFamily = (FontFamily)Application.Current.Resources["IconFont"],
                    FontSize = 17,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
            });
            var brandText = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            brandText.Children.Add(Ui.Text("Aria Boost", 16, "Text", FontWeights.Bold));
            brandText.Children.Add(Ui.Text("Oyun optimizasyonu", 11.5, "TextDim"));
            brand.Children.Add(brandText);
            sidebar.Children.Add(brand);

            AddNav(sidebar, "overview", Glyphs.Home, "Genel Bakış");
            AddNav(sidebar, "tweaks", Glyphs.Bolt, "Optimizasyonlar");
            AddNav(sidebar, "ping", Glyphs.Wifi, "Ping");
            AddNav(sidebar, "startup", Glyphs.Play, "Başlangıç");
            AddNav(sidebar, "cleaner", Glyphs.Delete, "Temizlik");

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var footer = new StackPanel();
            DockPanel.SetDock(footer, Dock.Bottom);
            revertAllButton = Ui.Ghost("Tümünü geri al", async (_, __) => await RevertAllAsync(), Glyphs.Undo);
            revertAllButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            revertAllButton.ToolTip = "Aria Boost'un yaptığı tüm optimizasyonları orijinal değerlerine döndürür.";
            footer.Children.Add(revertAllButton);
            var versionText = Ui.Text($"Sürüm {version.Major}.{version.Minor}.{version.Build}  ·  {state.Os.Name} {state.Os.DisplayVersion}", 11, "TextFaint");
            versionText.Margin = new Thickness(4, 12, 0, 0);
            footer.Children.Add(versionText);
            sidebar.Children.Add(footer);

            Grid.SetColumn(sidebarBorder, 0);
            root.Children.Add(sidebarBorder);

            // ---- İçerik ----
            var content = new DockPanel();
            restartBanner = BuildRestartBanner();
            DockPanel.SetDock(restartBanner, Dock.Top);
            content.Children.Add(restartBanner);
            content.Children.Add(host);
            Grid.SetColumn(content, 1);
            root.Children.Add(content);

            Content = root;

            state.RestartRequiredChanged += () => restartBanner.Visibility = Visibility.Visible;
            state.TweaksChanged += UpdateRevertButton;
            UpdateRevertButton();

            if (!state.Os.IsSupported)
            {
                Loaded += (_, __) => Ui.Error(this,
                    $"Aria Boost Windows 10 sürüm 1809 ve üstü için tasarlandı. Bu bilgisayar derleme {state.Os.Build} çalıştırıyor; " +
                    "bazı optimizasyonlar kullanılamayabilir.");
            }

            Navigate("overview");
        }

        public void Navigate(string id)
        {
            if (!pages.TryGetValue(id, out var page))
            {
                page = CreatePage(id);
                pages[id] = page;
            }
            host.Content = page;
            navButtons[id].IsChecked = true;
            (page as IPage)?.OnShown();
        }

        private FrameworkElement CreatePage(string id)
        {
            switch (id)
            {
                case "overview": return new OverviewPage(state, this);
                case "tweaks": return new TweaksPage(state, this);
                case "ping": return new PingPage(state, this);
                case "startup": return new StartupPage(this);
                case "cleaner": return new CleanerPage(this);
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }

        private void AddNav(DockPanel sidebar, string id, string glyph, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var icon = Ui.Icon(glyph, 15);
            icon.SetBinding(TextBlock.ForegroundProperty, new System.Windows.Data.Binding("Foreground")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(RadioButton), 1),
            });
            sp.Children.Add(icon);
            var text = new TextBlock { Text = label, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(text);

            var button = new RadioButton { Style = Ui.Style("NavButton"), Content = sp, GroupName = "nav" };
            button.Checked += (_, __) => { if (host.Content != pages.GetValueOrDefault(id)) Navigate(id); };
            DockPanel.SetDock(button, Dock.Top);
            sidebar.Children.Add(button);
            navButtons[id] = button;
        }

        private Border BuildRestartBanner()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = Ui.Icon(Glyphs.Warning, 15, "Warning");
            grid.Children.Add(icon);

            var text = Ui.Text("Bazı değişikliklerin etkinleşmesi için bilgisayarı yeniden başlatman gerekiyor.", 13, "Text", wrap: true);
            text.Margin = new Thickness(12, 0, 12, 0);
            text.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            var restart = Ui.Ghost("Şimdi yeniden başlat", (_, __) =>
            {
                if (Ui.Confirm(this, "Bilgisayar şimdi yeniden başlatılsın mı? Açık dosyalarını kaydettiğinden emin ol."))
                {
                    Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 0") { CreateNoWindow = true, UseShellExecute = false });
                }
            });
            Grid.SetColumn(restart, 2);
            grid.Children.Add(restart);

            return new Border
            {
                Background = Ui.Brush("WarningSoft"),
                BorderBrush = Ui.Brush("Border"),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(36, 10, 24, 10),
                Child = grid,
                Visibility = Visibility.Collapsed,
            };
        }

        private void UpdateRevertButton() => revertAllButton.IsEnabled = state.HasChanges;

        private async System.Threading.Tasks.Task RevertAllAsync()
        {
            if (!Ui.Confirm(this, "Aria Boost'un yaptığı tüm optimizasyonlar geri alınıp orijinal değerlerine döndürülsün mü?")) return;
            revertAllButton.IsEnabled = false;
            var errors = await state.RevertAllAsync();
            UpdateRevertButton();
            if (errors.Count > 0) Ui.Error(this, "Bazı ayarlar geri alınamadı:\n\n" + string.Join("\n", errors));
            else MessageBox.Show(this, "Tüm optimizasyonlar geri alındı.", "Aria Boost", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // Koyu başlık çubuğu (Windows 10 20H1+ ve Windows 11). Eski sürümlerde sessizce yok sayılır.
            var hwnd = new WindowInteropHelper(this).Handle;
            int on = 1;
            if (Native.DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                Native.DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }

        private static ImageSource LoadIcon()
        {
            try
            {
                using (var s = typeof(MainWindow).Assembly.GetManifestResourceStream("AriaBoost.icon.ico"))
                {
                    var decoder = new IconBitmapDecoder(s, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    return decoder.Frames[0];
                }
            }
            catch { return null; }
        }
    }

    /// <summary>Sayfa her gösterildiğinde verisini tazelemek için.</summary>
    internal interface IPage
    {
        void OnShown();
    }

    internal static class DictionaryExtensions
    {
        public static TValue GetValueOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> d, TKey key) =>
            d.TryGetValue(key, out var v) ? v : default(TValue);
    }
}
