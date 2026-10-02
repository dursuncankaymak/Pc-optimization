using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AriaBoost.Core
{
    /// <summary>İnternete çıkan etkin bağlantı.</summary>
    public sealed class ConnectionInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsWifi { get; set; }
        public long SpeedBps { get; set; }
        public IPAddress Gateway { get; set; }

        public string TypeLabel => IsWifi ? "Wi-Fi" : "Kablolu (Ethernet)";

        /// <summary>Varsayılan ağ geçidi olan, çalışan ilk fiziksel bağlantı.</summary>
        public static ConnectionInfo Detect()
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && IsPhysicalType(n.NetworkInterfaceType))
                .Select(n => new { Nic = n, Gateway = Ipv4Gateway(n) })
                .Where(x => x.Gateway != null)
                .ToList();
            var best = candidates.FirstOrDefault(x => x.Nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211) ?? candidates.FirstOrDefault();
            if (best == null) return null;
            return new ConnectionInfo
            {
                Id = best.Nic.Id,
                Name = best.Nic.Name,
                Description = best.Nic.Description,
                IsWifi = best.Nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211,
                SpeedBps = best.Nic.Speed,
                Gateway = best.Gateway,
            };
        }

        public static bool IsPhysicalType(NetworkInterfaceType t) =>
            t == NetworkInterfaceType.Ethernet || t == NetworkInterfaceType.GigabitEthernet ||
            t == NetworkInterfaceType.FastEthernetT || t == NetworkInterfaceType.FastEthernetFx ||
            t == NetworkInterfaceType.Ethernet3Megabit || t == NetworkInterfaceType.Wireless80211;

        private static IPAddress Ipv4Gateway(NetworkInterface nic)
        {
            try
            {
                return nic.GetIPProperties().GatewayAddresses
                    .Select(g => g.Address)
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
            }
            catch (NetworkInformationException) { return null; }
        }
    }

    public sealed class PingStats
    {
        public int Sent { get; set; }
        public int Received { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }

        /// <summary>Ardışık iki ölçüm arasındaki ortalama fark (ms) — "ping oynaklığı".</summary>
        public double Jitter { get; set; }

        /// <summary>Ölçümlerin %95'i bu değerin altında.</summary>
        public double P95 { get; set; }

        public double LossPercent => Sent == 0 ? 0 : 100.0 * (Sent - Received) / Sent;

        public static PingStats Compute(IReadOnlyList<long?> samples)
        {
            var ok = samples.Where(s => s.HasValue).Select(s => (double)s.Value).ToList();
            var stats = new PingStats { Sent = samples.Count, Received = ok.Count };
            if (ok.Count == 0) return stats;
            stats.Average = ok.Average();
            stats.Min = ok.Min();
            stats.Max = ok.Max();
            stats.Jitter = ok.Count < 2 ? 0 : ok.Zip(ok.Skip(1), (a, b) => Math.Abs(b - a)).Average();
            var sorted = ok.OrderBy(v => v).ToList();
            stats.P95 = sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(sorted.Count * 0.95) - 1)];
            return stats;
        }
    }

    public sealed class PingSample
    {
        public int Index { get; set; }
        public long? Gateway { get; set; }
        public long? Internet { get; set; }
    }

    public sealed class PingTestResult
    {
        public ConnectionInfo Connection { get; set; }
        public string Target { get; set; }
        public List<long?> GatewaySamples { get; } = new List<long?>();
        public List<long?> InternetSamples { get; } = new List<long?>();
        public PingStats Gateway => PingStats.Compute(GatewaySamples);
        public PingStats Internet => PingStats.Compute(InternetSamples);

        /// <summary>Test boyunca bu bilgisayarın ortalama indirme + yükleme trafiği (Mbit/s).</summary>
        public double BackgroundMbps { get; set; }

        /// <summary>Çalışan ve çok bant genişliği kullanabilen programlar (Steam, torrent vb.).</summary>
        public List<string> HeavyApps { get; } = new List<string>();
    }

    /// <summary>Ping testi: modeme (yerel ağ) ve internete aynı anda ping atar.</summary>
    public static class PingTester
    {
        private static readonly Dictionary<string, string> KnownDownloaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["steam"] = "Steam",
            ["steamwebhelper"] = null,
            ["EpicGamesLauncher"] = "Epic Games",
            ["EADesktop"] = "EA app",
            ["Battle.net"] = "Battle.net",
            ["upc"] = "Ubisoft Connect",
            ["RiotClientServices"] = "Riot Client",
            ["OneDrive"] = "OneDrive",
            ["Dropbox"] = "Dropbox",
            ["GoogleDriveFS"] = "Google Drive",
            ["qbittorrent"] = "qBittorrent",
            ["utorrent"] = "µTorrent",
            ["BitTorrent"] = "BitTorrent",
            ["Transmission"] = "Transmission",
            ["IDMan"] = "Internet Download Manager",
        };

        public static async Task<PingTestResult> RunAsync(string target, TimeSpan duration, TimeSpan interval,
            IProgress<PingSample> progress, CancellationToken token)
        {
            var result = new PingTestResult { Connection = ConnectionInfo.Detect(), Target = target };
            var nic = result.Connection == null ? null :
                NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n => n.Id == result.Connection.Id);
            long bytesBefore = SafeBytes(nic);
            var sw = Stopwatch.StartNew();

            int count = (int)(duration.TotalMilliseconds / interval.TotalMilliseconds);
            for (int i = 0; i < count && !token.IsCancellationRequested; i++)
            {
                var tickStart = sw.Elapsed;
                var gatewayTask = result.Connection?.Gateway != null ? PingOnce(result.Connection.Gateway.ToString()) : Task.FromResult<long?>(null);
                var internetTask = PingOnce(target);
                await Task.WhenAll(gatewayTask, internetTask);

                var sample = new PingSample { Index = i, Gateway = gatewayTask.Result, Internet = internetTask.Result };
                if (result.Connection?.Gateway != null) result.GatewaySamples.Add(sample.Gateway);
                result.InternetSamples.Add(sample.Internet);
                progress?.Report(sample);

                var wait = interval - (sw.Elapsed - tickStart);
                if (wait > TimeSpan.Zero)
                {
                    try { await Task.Delay(wait, token); }
                    catch (TaskCanceledException) { break; }
                }
            }

            long bytesAfter = SafeBytes(nic);
            if (bytesBefore >= 0 && bytesAfter >= bytesBefore && sw.Elapsed.TotalSeconds > 0)
                result.BackgroundMbps = (bytesAfter - bytesBefore) * 8 / 1e6 / sw.Elapsed.TotalSeconds;

            result.HeavyApps.AddRange(RunningHeavyApps());
            return result;
        }

        public static async Task<long?> PingOnce(string host)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, 1000);
                    return reply.Status == IPStatus.Success ? reply.RoundtripTime : (long?)null;
                }
            }
            catch (PingException) { return null; }
            catch (ArgumentException) { return null; }
        }

        public static List<string> RunningHeavyApps()
        {
            var found = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (KnownDownloaders.TryGetValue(p.ProcessName, out var label) && label != null) found.Add(label);
                }
                finally { p.Dispose(); }
            }
            return found.ToList();
        }

        private static long SafeBytes(NetworkInterface nic)
        {
            try
            {
                if (nic == null) return -1;
                var s = nic.GetIPv4Statistics();
                return s.BytesReceived + s.BytesSent;
            }
            catch (NetworkInformationException) { return -1; }
        }
    }

    public enum Verdict { Good, LocalNetwork, LineBusy, BeyondHome, NoInternet }

    public sealed class Diagnosis
    {
        public Verdict Verdict { get; set; }
        public string Headline { get; set; }
        public string Explanation { get; set; }
        public List<string> Advice { get; } = new List<string>();
    }

    /// <summary>Ping testi sonucunu yorumlar: sorun evdeki ağda mı, hatta mı, servis sağlayıcıda mı?</summary>
    public static class PingDiagnosis
    {
        // Kablolu bağlantıda modem pingi ~1 ms ve neredeyse sabittir; Wi-Fi'da birkaç ms normaldir.
        public const double LocalJitterLimit = 4;
        public const double LocalSpikeLimit = 25;
        public const double InternetJitterLimit = 5;
        public const double BusyLineMbps = 8;

        public static Diagnosis Analyze(PingTestResult r)
        {
            var d = new Diagnosis();
            var gw = r.Gateway;
            var net = r.Internet;
            bool wifi = r.Connection?.IsWifi == true;
            bool hasGateway = gw.Sent > 0 && gw.Received > 0;

            if (net.Received == 0)
            {
                d.Verdict = Verdict.NoInternet;
                d.Headline = "İnternete ping atılamadı";
                d.Explanation = $"{r.Target} adresinden hiç yanıt gelmedi. Bağlantını ya da yazdığın adresi kontrol et; bazı sunucular ping'e yanıt vermez.";
                return d;
            }

            bool localBad = hasGateway && (gw.Jitter > LocalJitterLimit || gw.Max > LocalSpikeLimit || gw.LossPercent > 0);
            bool internetBad = net.Jitter > InternetJitterLimit || net.LossPercent > 0 || net.Max - net.Min > 30;
            bool busy = r.BackgroundMbps > BusyLineMbps;

            if (localBad)
            {
                d.Verdict = Verdict.LocalNetwork;
                d.Headline = "Sorun evdeki ağda: bilgisayar ile modem arası";
                d.Explanation = $"Modeme bile ping oynak (ortalama {gw.Average:0} ms, en yüksek {gw.Max:0} ms" +
                                (gw.LossPercent > 0 ? $", %{gw.LossPercent:0} kayıp" : "") +
                                "). İnternet sağlayıcın ya da oyun sunucusu değil, ev içindeki bağlantı ping'i oynatıyor.";
                if (wifi)
                {
                    d.Advice.Add("En etkili çözüm: bilgisayarı modeme Ethernet kablosuyla bağla. Kabloda bu dalgalanma neredeyse tamamen kaybolur.");
                    d.Advice.Add("Kablo mümkün değilse modemin 5 GHz ağına bağlan ve modemle arandaki duvar sayısını azalt.");
                    d.Advice.Add("Aşağıdaki \"Oyun sırasında Wi-Fi taramasını durdur\" ayarını aç: Windows'un arka planda ağ araması dakikada bir ping sıçramasına yol açar.");
                }
                else
                {
                    d.Advice.Add("Kabloyu ve modemdeki portu değiştirmeyi dene; hasarlı kablo kayba yol açar.");
                    d.Advice.Add("Optimizasyonlar → Ağ bölümündeki \"Ağ kartı güç tasarrufunu kapat\" ayarını uygula.");
                }
                if (busy) d.Advice.Add($"Test sırasında bu bilgisayar {r.BackgroundMbps:0} Mbit/s veri aktarıyordu; bu da ping'i oynatır.");
            }
            else if (internetBad && busy)
            {
                d.Verdict = Verdict.LineBusy;
                d.Headline = "Hat dolu: arka planda indirme/yükleme var";
                d.Explanation = $"Modeme bağlantın sağlam, ama test sırasında bu bilgisayar {r.BackgroundMbps:0} Mbit/s veri aktarıyordu. " +
                                "Hat dolunca oyun paketleri modemde sıraya girer ve ping dalgalanır (bufferbloat).";
                d.Advice.Add("Oynarken indirmeleri duraklat veya indirme hızını sınırla.");
            }
            else if (internetBad)
            {
                d.Verdict = Verdict.BeyondHome;
                d.Headline = "Sorun evin dışında: hat ya da servis sağlayıcı";
                d.Explanation = (hasGateway ? $"Modeme bağlantın sabit ({gw.Average:0} ms), " : "") +
                                $"ama internete giden ping {net.Min:0}–{net.Max:0} ms arasında oynuyor. Bu, modemden sonraki kısımda oluşuyor.";
                d.Advice.Add("Evdeki başka cihazlar (TV'de 4K yayın, telefonda indirme, başka bilgisayar) hattı dolduruyor olabilir; onlar kapalıyken testi tekrarla.");
                d.Advice.Add("Modemin ayarlarında QoS / \"oyun önceliği\" / SQM özelliği varsa aç; hat doluyken bile oyun paketlerini öne alır.");
                d.Advice.Add("Sorun sürekliyse ve evde başka trafik yokken de oluyorsa servis sağlayıcını ara; hat değerlerinin (SNR, hata sayısı) kontrol edilmesini iste.");
            }
            else
            {
                d.Verdict = Verdict.Good;
                d.Headline = "Bağlantın sağlıklı";
                d.Explanation = $"İnternet pingi {net.Min:0}–{net.Max:0} ms arasında, dalgalanma {net.Jitter:0.0} ms. " +
                                "Oyunda yine de oynak ping görüyorsan sorun büyük olasılıkla oyun sunucusunun yolunda; oyun sunucusunun IP adresiyle testi tekrarlayabilirsin.";
            }

            if (r.HeavyApps.Count > 0 && d.Verdict != Verdict.Good)
                d.Advice.Add("Açık olan ve arka planda indirme yapabilen programlar: " + string.Join(", ", r.HeavyApps) + ".");

            return d;
        }
    }
}
