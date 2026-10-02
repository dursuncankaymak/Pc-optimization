using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    /// <summary>Sistem bilgisi, durum özeti ve "önerilenleri uygula".</summary>
    internal sealed class OverviewPage : ContentControl, IPage
    {
        private readonly AppState state;
        private readonly Window owner;

        private readonly UniformGrid statCards = new UniformGrid { Columns = 4 };
        private readonly StackPanel statusRows = new StackPanel();
        private readonly TextBlock heroCount = Ui.Text("", 34, "Text", FontWeights.Bold);
        private readonly TextBlock heroDetail = Ui.Text("", 13.5, "TextDim", wrap: true);
        private readonly Border progressFill = new Border { CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left };
        private readonly Grid progressTrack = new Grid { Height = 8 };
        private readonly Button applyButton;
        private readonly CheckBox restorePointCheck;
        private readonly TextBlock busyText = Ui.Text("", 13, "TextDim", wrap: true);
        private SystemInfo info;

        public OverviewPage(AppState state, Window owner)
        {
            this.state = state;
            this.owner = owner;

            var body = new StackPanel();
            body.Children.Add(Ui.Header("Genel Bakış", $"{state.Os.FullName} · derleme {state.Os.BuildLabel}"));

            // ---- Önerilenler kartı ----
            applyButton = Ui.Primary("Önerilenleri uygula", async (_, __) => await ApplyRecommendedAsync());
            restorePointCheck = new CheckBox
            {
                Style = Ui.Style("Check"),
                IsChecked = true,
                Content = Ui.Text("Önce sistem geri yükleme noktası oluştur", 13, "TextDim"),
                Margin = new Thickness(18, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            progressFill.Background = Ui.Brush("AccentGradient");
            progressTrack.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Ui.Brush("Border") });
            progressTrack.Children.Add(progressFill);
            progressTrack.SizeChanged += (_, __) => UpdateHero();

            var hero = new StackPanel();
            hero.Children.Add(Ui.Text("ÖNERİLEN OPTİMİZASYONLAR", 11.5, "Accent2", FontWeights.SemiBold));
            heroCount.Margin = new Thickness(0, 8, 0, 2);
            hero.Children.Add(heroCount);
            hero.Children.Add(heroDetail);
            progressTrack.Margin = new Thickness(0, 16, 0, 20);
            hero.Children.Add(progressTrack);
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            actions.Children.Add(applyButton);
            actions.Children.Add(restorePointCheck);
            hero.Children.Add(actions);
            busyText.Margin = new Thickness(0, 12, 0, 0);
            busyText.Visibility = Visibility.Collapsed;
            hero.Children.Add(busyText);

            var heroCard = Ui.Card(hero);
            heroCard.Background = Ui.Brush("HeroGradient");
            heroCard.Padding = new Thickness(26, 24, 26, 24);
            body.Children.Add(heroCard);

            // ---- Donanım ----
            var hwTitle = Ui.Text("Sistemin", 15, "Text", FontWeights.SemiBold);
            hwTitle.Margin = new Thickness(2, 28, 0, 12);
            body.Children.Add(hwTitle);
            statCards.Margin = new Thickness(-6, 0, -6, 0);
            body.Children.Add(statCards);

            // ---- Durum ----
            var statusTitle = Ui.Text("Durum", 15, "Text", FontWeights.SemiBold);
            statusTitle.Margin = new Thickness(2, 28, 0, 12);
            body.Children.Add(statusTitle);
            var statusCard = Ui.Card(statusRows);
            statusCard.Padding = new Thickness(20, 8, 20, 8);
            body.Children.Add(statusCard);

            Content = Ui.Page(body);
            state.TweaksChanged += () => { UpdateHero(); _ = LoadInfoAsync(); };
        }

        public void OnShown()
        {
            UpdateHero();
            _ = LoadInfoAsync();
        }

        private async Task LoadInfoAsync()
        {
            info = await Task.Run(() => SystemInfo.Collect());
            RenderStats();
            RenderStatus();
        }

        private void UpdateHero()
        {
            var rec = state.Recommended.ToList();
            int applied = rec.Count(t => state.SafeState(t) == TweakState.Applied);
            int missing = rec.Count - applied;
            heroCount.Text = $"{applied} / {rec.Count}";
            heroDetail.Text = missing == 0
                ? "Önerilen optimizasyonların hepsi uygulanmış. Tek tek ayarlar için Optimizasyonlar sayfasına bak."
                : $"{missing} önerilen optimizasyon henüz uygulanmamış. Hepsi tek tıkla uygulanır ve istediğin zaman geri alınabilir.";
            applyButton.IsEnabled = missing > 0;
            double ratio = rec.Count == 0 ? 0 : (double)applied / rec.Count;
            progressFill.Width = Math.Max(0, progressTrack.ActualWidth * ratio);
        }

        private async Task ApplyRecommendedAsync()
        {
            applyButton.IsEnabled = false;
            restorePointCheck.IsEnabled = false;
            busyText.Visibility = Visibility.Visible;
            try
            {
                if (restorePointCheck.IsChecked == true)
                {
                    busyText.Text = "Sistem geri yükleme noktası oluşturuluyor… (bu bir dakika sürebilir)";
                    var rp = await Task.Run(() => RestorePoint.Create("Aria Boost - optimizasyon öncesi"));
                    if (!rp.Success &&
                        !Ui.Confirm(owner, rp.Message + "\n\nAria Boost her ayarın orijinal değerini kendisi de yedekler; " +
                                           "\"Tümünü geri al\" ile geri dönebilirsin. Yine de devam edilsin mi?"))
                    {
                        return;
                    }
                }

                busyText.Text = "Optimizasyonlar uygulanıyor…";
                var errors = await state.ApplyRecommendedAsync();
                if (errors.Count > 0)
                {
                    Ui.Error(owner, "Bazı optimizasyonlar uygulanamadı:\n\n" + string.Join("\n\n", errors));
                }
            }
            finally
            {
                busyText.Visibility = Visibility.Collapsed;
                restorePointCheck.IsEnabled = true;
                UpdateHero();
            }
        }

        private void RenderStats()
        {
            statCards.Children.Clear();
            var os = info.Os;
            statCards.Children.Add(Stat("İŞLETİM SİSTEMİ", $"{os.Name} {os.DisplayVersion}", $"Derleme {os.BuildLabel}"));
            statCards.Children.Add(Stat("İŞLEMCİ", ShortCpu(info.Cpu), $"{info.LogicalCores} iş parçacığı"));
            var gpu = info.Gpus.FirstOrDefault() ?? "Bulunamadı";
            statCards.Children.Add(Stat("EKRAN KARTI", gpu, info.Gpus.Count > 1 ? $"+{info.Gpus.Count - 1} ekran kartı daha" : " "));
            var usedPct = info.TotalRam == 0 ? 0 : 100.0 * (info.TotalRam - info.AvailableRam) / info.TotalRam;
            statCards.Children.Add(Stat("BELLEK", $"{Math.Round(info.TotalRam / 1073741824.0)} GB RAM",
                $"%{usedPct:0} kullanımda · C: {SystemInfo.FormatBytes(info.SystemDriveFree)} boş"));
        }

        private static string ShortCpu(string cpu) =>
            (cpu ?? "").Replace("(R)", "").Replace("(TM)", "").Replace(" CPU", "").Replace("  ", " ").Trim();

        private static FrameworkElement Stat(string label, string value, string sub)
        {
            var sp = new StackPanel();
            sp.Children.Add(Ui.Text(label, 11, "TextDim", FontWeights.SemiBold));
            var v = Ui.Text(value, 15, "Text", FontWeights.SemiBold);
            v.Margin = new Thickness(0, 8, 0, 4);
            v.ToolTip = value;
            sp.Children.Add(v);
            sp.Children.Add(Ui.Text(sub, 12, "TextDim"));
            var card = Ui.Card(sp, new Thickness(6, 0, 6, 0));
            card.Padding = new Thickness(18, 16, 18, 16);
            return card;
        }

        private void RenderStatus()
        {
            statusRows.Children.Clear();
            var byId = state.Tweaks.ToDictionary(t => t.Id);

            bool perfPlan = byId.TryGetValue("power-plan", out var pp) && state.SafeState(pp) == TweakState.Applied;
            AddStatus("Güç planı", info.PowerPlan, perfPlan ? "Success" : "Warning",
                perfPlan ? null : "Dengeli plan işlemciyi güç tasarrufu için yavaşlatabilir.");

            bool gameMode = byId.TryGetValue("game-mode", out var gm) && state.SafeState(gm) == TweakState.Applied;
            AddStatus("Oyun Modu", gameMode ? "Açık" : "Kapalı", gameMode ? "Success" : "Warning", null);

            if (byId.TryGetValue("hags", out var hags) && hags.UnsupportedReason(state.Os) == null)
            {
                bool on = state.SafeState(hags) == TweakState.Applied;
                AddStatus("GPU zamanlaması", on ? "Açık" : "Kapalı / sürücü varsayılanı", on ? "Success" : "TextFaint", null);
            }

            bool dvrOff = byId.TryGetValue("game-dvr", out var dvr) && state.SafeState(dvr) == TweakState.Applied;
            AddStatus("Arka plan oyun kaydı", dvrOff ? "Kapalı" : "Açık", dvrOff ? "Success" : "Warning", null);

            if (info.MemoryIntegrity == true)
            {
                var open = new Button
                {
                    Style = Ui.Style("GhostButton"),
                    Content = "Windows Güvenliği'nde aç",
                    Padding = new Thickness(12, 6, 12, 6),
                    FontSize = 12,
                };
                open.Click += (_, __) =>
                {
                    try { Process.Start(new ProcessStartInfo("windowsdefender://coreisolation") { UseShellExecute = true }); }
                    catch (Exception ex) { Ui.Error(owner, ex.Message); }
                };
                AddStatus("Bellek Bütünlüğü", "Açık", "Accent2",
                    "Önemli bir güvenlik özelliğidir; bazı oyunlarda birkaç FPS'e mal olabilir. Aria Boost güvenlik ayarlarını değiştirmez — karar senin.",
                    open);
            }
            else
            {
                AddStatus("Bellek Bütünlüğü", "Kapalı", "TextFaint", null);
            }
        }

        private void AddStatus(string label, string value, string dot, string note, FrameworkElement action = null)
        {
            var grid = new Grid { Margin = new Thickness(0, 12, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var left = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
            left.Children.Add(Ui.Dot(dot));
            var l = Ui.Text(label, 13.5, "TextDim");
            l.Margin = new Thickness(12, 0, 0, 0);
            left.Children.Add(l);
            grid.Children.Add(left);

            var right = new StackPanel();
            right.Children.Add(Ui.Text(value, 13.5, "Text", FontWeights.SemiBold));
            if (note != null)
            {
                var n = Ui.Text(note, 12.5, "TextDim", wrap: true);
                n.Margin = new Thickness(0, 4, 16, 0);
                right.Children.Add(n);
            }
            Grid.SetColumn(right, 1);
            grid.Children.Add(right);

            if (action != null)
            {
                action.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(action, 2);
                grid.Children.Add(action);
            }

            if (statusRows.Children.Count > 0)
                statusRows.Children.Add(new Border { Height = 1, Background = Ui.Brush("Border") });
            statusRows.Children.Add(grid);
        }
    }
}
