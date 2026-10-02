using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    /// <summary>
    /// Ping testi ve teşhis. Modeme ve internete aynı anda ping atarak oynaklığın evdeki
    /// ağdan mı (Wi-Fi, kablo), hattın dolmasından mı yoksa evin dışından mı geldiğini bulur.
    /// </summary>
    internal sealed class PingPage : ContentControl, IPage
    {
        private static readonly TimeSpan Duration = TimeSpan.FromSeconds(20);
        private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);

        private readonly AppState state;
        private readonly Window owner;
        private readonly TextBlock connectionText = Ui.Text("", 13.5, "Text", FontWeights.SemiBold);
        private readonly TextBlock connectionSub = Ui.Text("", 12.5, "TextDim");
        private readonly TextBox targetBox;
        private readonly Button startButton;
        private readonly TextBlock liveText = Ui.Text("", 13, "TextDim");
        private readonly Canvas chart = new Canvas { Height = 170, ClipToBounds = true };
        private readonly Polyline internetLine = new Polyline { StrokeThickness = 2, StrokeLineJoin = PenLineJoin.Round };
        private readonly Polyline gatewayLine = new Polyline { StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round };
        private readonly TextBlock chartMax = Ui.Text("", 11, "TextFaint");
        private readonly StackPanel results = new StackPanel();
        private readonly Border wifiCard;
        private readonly CheckBox wifiToggle;
        private readonly List<long?> internet = new List<long?>();
        private readonly List<long?> gateway = new List<long?>();
        private ConnectionInfo connection;
        private CancellationTokenSource cts;

        public PingPage(AppState state, Window owner)
        {
            this.state = state;
            this.owner = owner;

            var body = new StackPanel();
            body.Children.Add(Ui.Header("Ping",
                "Oynak ping'in nereden geldiğini ölçer: bilgisayar ile modem arasından mı (Wi-Fi), hattın dolmasından mı, yoksa evin dışından mı. " +
                "Hiçbir program internet hızını ya da servis sağlayıcının hattını değiştiremez; ama sorunun bilgisayardaki kısmı düzeltilebilir."));

            // ---- Bağlantı + test ----
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition());
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var conn = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            conn.Children.Add(Ui.Text("BAĞLANTI", 11, "TextDim", FontWeights.SemiBold));
            connectionText.Margin = new Thickness(0, 6, 0, 2);
            conn.Children.Add(connectionText);
            conn.Children.Add(connectionSub);
            top.Children.Add(conn);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var targetLabel = Ui.Text("Hedef", 12.5, "TextDim");
            targetLabel.VerticalAlignment = VerticalAlignment.Center;
            targetLabel.Margin = new Thickness(0, 0, 10, 0);
            controls.Children.Add(targetLabel);
            targetBox = new TextBox
            {
                Text = "1.1.1.1",
                Width = 170,
                FontSize = 13.5,
                Padding = new Thickness(10, 8, 10, 8),
                Background = Ui.Brush("Bg"),
                Foreground = Ui.Brush("Text"),
                BorderBrush = Ui.Brush("Border"),
                CaretBrush = Ui.Brush("Text"),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "İnternet hedefi. Oyun sunucusunun IP adresini biliyorsan onu da yazabilirsin.",
            };
            controls.Children.Add(targetBox);
            startButton = Ui.Primary("Testi başlat", async (_, __) => await StartOrStopAsync());
            startButton.Margin = new Thickness(12, 0, 0, 0);
            startButton.MinWidth = 140;
            controls.Children.Add(startButton);
            Grid.SetColumn(controls, 1);
            top.Children.Add(controls);

            // ---- Grafik ----
            internetLine.Stroke = Ui.Brush("AccentGradient");
            gatewayLine.Stroke = Ui.Brush("TextFaint");
            chart.Children.Add(gatewayLine);
            chart.Children.Add(internetLine);
            Canvas.SetLeft(chartMax, 4);
            Canvas.SetTop(chartMax, 2);
            chart.Children.Add(chartMax);
            chart.SizeChanged += (_, __) => Redraw();

            var legend = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            legend.Children.Add(LegendItem("AccentGradient", "İnternet"));
            legend.Children.Add(LegendItem("TextFaint", "Modem (yerel ağ)"));
            liveText.Margin = new Thickness(24, 0, 0, 0);
            legend.Children.Add(liveText);

            var chartBorder = new Border
            {
                Background = Ui.Brush("Bg"),
                CornerRadius = new CornerRadius(10),
                BorderBrush = Ui.Brush("Border"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 20, 0, 0),
                Child = chart,
            };

            var testPanel = new StackPanel();
            testPanel.Children.Add(top);
            testPanel.Children.Add(chartBorder);
            testPanel.Children.Add(legend);
            body.Children.Add(Ui.Card(testPanel));

            results.Margin = new Thickness(0, 18, 0, 0);
            body.Children.Add(results);

            // ---- Wi-Fi taraması ----
            var wifiGrid = new Grid();
            wifiGrid.ColumnDefinitions.Add(new ColumnDefinition());
            wifiGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var wifiText = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            wifiText.Children.Add(Ui.Text("Oyun sırasında Wi-Fi taramasını durdur", 14.5, "Text", FontWeights.SemiBold));
            var wifiDesc = Ui.Text(
                "Windows, Wi-Fi'a bağlıyken bile yaklaşık dakikada bir çevredeki ağları tarar; tarama anında ping birkaç saniyeliğine sıçrar. " +
                "Bu ayar açıkken tarama durur, bağlantın devam eder. Bağlantı koparsa Windows kendiliğinden yeniden bağlanmaz; " +
                "bu yüzden Aria Boost kapanınca tarama otomatik olarak geri açılır.", 13, "TextDim", wrap: true);
            wifiDesc.Margin = new Thickness(0, 6, 0, 0);
            wifiDesc.LineHeight = 19;
            wifiText.Children.Add(wifiDesc);
            wifiGrid.Children.Add(wifiText);
            wifiToggle = new CheckBox { Style = Ui.Style("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center };
            wifiToggle.Click += (_, __) => ToggleWifiScan();
            Grid.SetColumn(wifiToggle, 1);
            wifiGrid.Children.Add(wifiToggle);
            wifiCard = Ui.Card(wifiGrid, new Thickness(0, 18, 0, 0));
            wifiCard.Visibility = Visibility.Collapsed;
            body.Children.Add(wifiCard);

            var tip = Ui.Text("Ağ kartı güç tasarrufu ve Windows Update'in yükleme yapması gibi kalıcı ayarlar Optimizasyonlar → Ağ bölümündedir.", 12.5, "TextFaint", wrap: true);
            tip.Margin = new Thickness(2, 18, 0, 0);
            body.Children.Add(tip);

            Content = Ui.Page(body);
        }

        public void OnShown() => _ = DetectAsync();

        private async Task DetectAsync()
        {
            connection = await Task.Run(() => ConnectionInfo.Detect());
            if (connection == null)
            {
                connectionText.Text = "Bağlantı bulunamadı";
                connectionSub.Text = "Bilgisayar internete bağlı görünmüyor.";
                wifiCard.Visibility = Visibility.Collapsed;
                return;
            }
            connectionText.Text = $"{connection.TypeLabel} · {connection.Name}";
            var speed = connection.SpeedBps > 0 ? $" · {connection.SpeedBps / 1_000_000} Mbit/s bağlantı" : "";
            connectionSub.Text = $"{connection.Description}{speed} · Modem: {connection.Gateway}";
            wifiCard.Visibility = connection.IsWifi ? Visibility.Visible : Visibility.Collapsed;
            wifiToggle.IsChecked = WifiScan.IsStopped(state.Store);
        }

        private async Task StartOrStopAsync()
        {
            if (cts != null)
            {
                cts.Cancel();
                return;
            }

            var target = targetBox.Text.Trim();
            if (target.Length == 0) target = targetBox.Text = "1.1.1.1";
            internet.Clear();
            gateway.Clear();
            results.Children.Clear();
            Redraw();

            cts = new CancellationTokenSource();
            startButton.Content = "Durdur";
            targetBox.IsEnabled = false;
            int total = (int)(Duration.TotalMilliseconds / Interval.TotalMilliseconds);
            var progress = new Progress<PingSample>(s =>
            {
                internet.Add(s.Internet);
                if (connection?.Gateway != null) gateway.Add(s.Gateway);
                var last = s.Internet.HasValue ? $"{s.Internet} ms" : "yanıt yok";
                liveText.Text = $"Ölçülüyor… {Math.Max(0, (total - s.Index - 1) * Interval.TotalSeconds):0} sn  ·  son: {last}";
                Redraw();
            });

            try
            {
                var result = await PingTester.RunAsync(target, Duration, Interval, progress, cts.Token);
                if (result.InternetSamples.Count >= 8) ShowResult(result);
                liveText.Text = "";
            }
            catch (Exception ex)
            {
                Ui.Error(owner, "Test yapılamadı: " + ex.Message);
            }
            finally
            {
                cts.Dispose();
                cts = null;
                startButton.Content = "Testi başlat";
                targetBox.IsEnabled = true;
            }
        }

        private void Redraw()
        {
            double w = chart.ActualWidth, h = chart.ActualHeight;
            if (w <= 0 || h <= 0) return;
            var all = internet.Concat(gateway).Where(v => v.HasValue).Select(v => (double)v.Value).ToList();
            double max = Math.Max(20, all.Count == 0 ? 20 : all.Max() * 1.25);
            int total = (int)(Duration.TotalMilliseconds / Interval.TotalMilliseconds);
            chartMax.Text = $"{max:0} ms";

            PointCollection Points(List<long?> data)
            {
                var pts = new PointCollection();
                for (int i = 0; i < data.Count; i++)
                {
                    // Yanıt gelmeyen ölçüm grafiğin tepesinde gösterilir.
                    double v = data[i] ?? max;
                    pts.Add(new Point(8 + (w - 16) * i / Math.Max(1, total - 1), h - 8 - (h - 24) * Math.Min(v, max) / max));
                }
                return pts;
            }
            internetLine.Points = Points(internet);
            gatewayLine.Points = Points(gateway);
        }

        private void ShowResult(PingTestResult r)
        {
            results.Children.Clear();
            var diag = PingDiagnosis.Analyze(r);

            // ---- Sayılar ----
            var stats = new Grid();
            stats.ColumnDefinitions.Add(new ColumnDefinition());
            stats.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            stats.ColumnDefinitions.Add(new ColumnDefinition());
            var netCard = StatsCard($"İNTERNET ({r.Target})", r.Internet);
            stats.Children.Add(netCard);
            if (r.GatewaySamples.Count > 0)
            {
                var gwCard = StatsCard($"MODEM ({r.Connection.Gateway})", r.Gateway);
                Grid.SetColumn(gwCard, 2);
                stats.Children.Add(gwCard);
            }
            results.Children.Add(stats);

            // ---- Teşhis ----
            string color = diag.Verdict == Verdict.Good ? "Success" : diag.Verdict == Verdict.NoInternet ? "Danger" : "Warning";
            var d = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            head.Children.Add(Ui.Dot(color, 10));
            var headline = Ui.Text(diag.Headline, 16, "Text", FontWeights.SemiBold);
            headline.Margin = new Thickness(12, 0, 0, 0);
            head.Children.Add(headline);
            d.Children.Add(head);
            var expl = Ui.Text(diag.Explanation, 13.5, "TextDim", wrap: true);
            expl.Margin = new Thickness(22, 8, 0, 0);
            expl.LineHeight = 20;
            d.Children.Add(expl);
            foreach (var advice in diag.Advice)
            {
                var line = new Grid { Margin = new Thickness(22, 10, 0, 0) };
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
                line.ColumnDefinitions.Add(new ColumnDefinition());
                line.Children.Add(Ui.Text("→", 13.5, "Accent2"));
                var t = Ui.Text(advice, 13.5, "Text", wrap: true);
                t.LineHeight = 20;
                Grid.SetColumn(t, 1);
                line.Children.Add(t);
                d.Children.Add(line);
            }
            results.Children.Add(Ui.Card(d, new Thickness(0, 18, 0, 0)));
        }

        private static FrameworkElement StatsCard(string title, PingStats s)
        {
            var grid = new Grid();
            for (int i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var t = Ui.Text(title, 11, "TextDim", FontWeights.SemiBold);
            Grid.SetColumnSpan(t, 4);
            grid.Children.Add(t);

            void Cell(int col, string label, string value, string brush)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 12, 8, 0) };
                sp.Children.Add(Ui.Text(value, 20, brush, FontWeights.Bold));
                sp.Children.Add(Ui.Text(label, 12, "TextDim"));
                Grid.SetRow(sp, 1);
                Grid.SetColumn(sp, col);
                grid.Children.Add(sp);
            }

            if (s.Received == 0)
            {
                Cell(0, "yanıt yok", "—", "Danger");
            }
            else
            {
                Cell(0, "ortalama", $"{s.Average:0} ms", "Text");
                Cell(1, "en düşük – en yüksek", $"{s.Min:0}–{s.Max:0}", "Text");
                Cell(2, "dalgalanma", $"{s.Jitter:0.0} ms", s.Jitter > 5 ? "Warning" : "Success");
                Cell(3, "kayıp", $"%{s.LossPercent:0}", s.LossPercent > 0 ? "Danger" : "Success");
            }
            return Ui.Card(grid);
        }

        private static FrameworkElement LegendItem(string brush, string label)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
            sp.Children.Add(new Border { Width = 14, Height = 3, CornerRadius = new CornerRadius(2), Background = Ui.Brush(brush), VerticalAlignment = VerticalAlignment.Center });
            var t = Ui.Text(label, 12, "TextDim");
            t.Margin = new Thickness(8, 0, 0, 0);
            sp.Children.Add(t);
            return sp;
        }

        private void ToggleWifiScan()
        {
            if (connection == null || !connection.IsWifi) return;
            try
            {
                if (wifiToggle.IsChecked == true) WifiScan.Stop(connection.Name, state.Store);
                else WifiScan.Resume(connection.Name, state.Store);
            }
            catch (Exception ex)
            {
                Ui.Error(owner, Ui.Describe(ex));
            }
            wifiToggle.IsChecked = WifiScan.IsStopped(state.Store);
        }
    }
}
