using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    /// <summary>Açılışta başlayan programlar.</summary>
    internal sealed class StartupPage : ContentControl, IPage
    {
        private readonly Window owner;
        private readonly StackPanel list = new StackPanel();
        private readonly TextBlock summary = Ui.Text("", 13, "TextDim");
        private readonly Dictionary<string, ImageSource> iconCache = new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);

        public StartupPage(Window owner)
        {
            this.owner = owner;
            var body = new StackPanel();
            body.Children.Add(Ui.Header("Başlangıç",
                "Bilgisayar açılırken kendiliğinden başlayan programlar. Gereksizleri kapatmak açılışı hızlandırır ve oyun sırasında " +
                "arka planda daha az program çalışır. Program silinmez; Görev Yöneticisi'ndeki \"Başlangıç uygulamaları\" ile aynı ayardır."));

            var bar = new DockPanel { Margin = new Thickness(2, 0, 0, 12) };
            var refresh = Ui.Ghost("Yenile", async (_, __) => await LoadAsync(), Glyphs.Refresh);
            DockPanel.SetDock(refresh, Dock.Right);
            bar.Children.Add(refresh);
            summary.VerticalAlignment = VerticalAlignment.Center;
            bar.Children.Add(summary);
            body.Children.Add(bar);

            var card = Ui.Card(list);
            card.Padding = new Thickness(20, 4, 20, 4);
            body.Children.Add(card);
            Content = Ui.Page(body);
        }

        public void OnShown() => _ = LoadAsync();

        private async Task LoadAsync()
        {
            List<StartupItem> items;
            try
            {
                items = await Task.Run(() => StartupManager.List());
            }
            catch (Exception ex)
            {
                Ui.Error(owner, "Başlangıç programları okunamadı: " + ex.Message);
                return;
            }

            list.Children.Clear();
            int enabled = items.Count(i => i.Enabled);
            summary.Text = items.Count == 0 ? "" : $"{items.Count} program · {enabled} tanesi açılışta başlıyor";

            if (items.Count == 0)
            {
                var empty = Ui.Text("Açılışta başlayan program yok.", 13.5, "TextDim");
                empty.Margin = new Thickness(0, 18, 0, 18);
                list.Children.Add(empty);
                return;
            }

            foreach (var item in items.OrderByDescending(i => i.Enabled).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (list.Children.Count > 0) list.Children.Add(new Border { Height = 1, Background = Ui.Brush("Border") });
                list.Children.Add(BuildRow(item));
            }
        }

        private FrameworkElement BuildRow(StartupItem item)
        {
            var grid = new Grid { Margin = new Thickness(0, 12, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var icon = GetIcon(item.ExecutablePath);
            FrameworkElement iconEl = icon != null
                ? (FrameworkElement)new Image { Source = icon, Width = 28, Height = 28 }
                : new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(7), Background = Ui.Brush("Border") };
            iconEl.HorizontalAlignment = HorizontalAlignment.Left;
            iconEl.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(iconEl);

            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            text.Children.Add(Ui.Text(item.Name, 14, "Text", FontWeights.SemiBold));
            var cmd = Ui.Text(item.Command, 12, "TextFaint");
            cmd.Margin = new Thickness(0, 3, 0, 0);
            cmd.ToolTip = item.Command;
            text.Children.Add(cmd);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            var badge = Ui.Badge(item.SourceLabel, "TextDim", "Border");
            badge.Margin = new Thickness(0, 0, 20, 0);
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            var toggle = new CheckBox { Style = Ui.Style("ToggleSwitch"), IsChecked = item.Enabled, VerticalAlignment = VerticalAlignment.Center };
            toggle.ToolTip = "Açılışta başlasın";
            toggle.Click += (_, __) =>
            {
                try
                {
                    StartupManager.SetEnabled(item, toggle.IsChecked == true);
                }
                catch (Exception ex)
                {
                    toggle.IsChecked = item.Enabled;
                    Ui.Error(owner, $"\"{item.Name}\" değiştirilemedi: {ex.Message}");
                }
            };
            Grid.SetColumn(toggle, 3);
            grid.Children.Add(toggle);
            return grid;
        }

        private ImageSource GetIcon(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (iconCache.TryGetValue(path, out var cached)) return cached;
            ImageSource result = null;
            try
            {
                if (File.Exists(path))
                {
                    using (var icon = System.Drawing.Icon.ExtractAssociatedIcon(path))
                    {
                        if (icon != null)
                        {
                            var src = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            src.Freeze();
                            result = src;
                        }
                    }
                }
            }
            catch
            {
                // simge alınamazsa yer tutucu gösterilir
            }
            iconCache[path] = result;
            return result;
        }
    }
}
