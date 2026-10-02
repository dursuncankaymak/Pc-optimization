using System;
using System.Linq;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace AriaBoost.Core
{
    /// <summary>
    /// Etkin fiziksel ağ kartlarında güç tasarrufu özelliklerini kapatır:
    ///  - Enerji Verimli Ethernet (*EEE) ve Realtek/Intel'in "Green Ethernet" benzeri seçenekleri:
    ///    hat boştayken kartı uyutur, uyanırken gecikme sıçramasına ve bazı kartlarda kopmalara yol açar.
    ///  - "Güç tasarrufu için bilgisayar bu aygıtı kapatabilir" (PnPCapabilities).
    /// Yalnızca sürücünün gerçekten sunduğu ve "0 = kapalı" seçeneği olan ayarlara dokunulur.
    /// Ayarlar sürücü yeniden yüklendiğinde (yeniden başlatınca) etkinleşir.
    /// </summary>
    public sealed class NetworkAdapterPowerTweak : RegistryTweak
    {
        private const string ClassPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
        private const int NcfPhysical = 0x4;
        private const int PnPDisablePowerOff = 24; // 0x10 | 0x8: "aygıtı kapatabilir" seçeneğini devre dışı bırak

        // Standart (*EEE) ve yaygın üretici anahtar kelimeleri; hepsinde "0" kapalı demektir.
        private static readonly string[] PowerKeywords =
        {
            "*EEE", "EEELinkAdvertisement", "EnableGreenEthernet", "AdvancedEEE", "PowerSavingMode", "GigaLite",
        };

        public NetworkAdapterPowerTweak()
        {
            try { Discover(); }
            catch (Exception) { Values.Clear(); }
        }

        public override string UnsupportedReason(OsInfo os) =>
            base.UnsupportedReason(os) ?? (Values.Count == 0 ? "Ayarlanabilir ağ kartı bulunamadı" : null);

        private void Discover()
        {
            var activeIds = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && ConnectionInfo.IsPhysicalType(n.NetworkInterfaceType))
                .Select(n => n.Id)
                .ToList();

            using (var root = Reg.OpenBase(Hive.LocalMachine))
            using (var cls = root.OpenSubKey(ClassPath, false))
            {
                if (cls == null) return;
                foreach (var sub in cls.GetSubKeyNames())
                {
                    if (sub.Length != 4 || !sub.All(char.IsDigit)) continue;
                    RegistryKey key;
                    try { key = cls.OpenSubKey(sub, false); }
                    catch (System.Security.SecurityException) { continue; }
                    using (key)
                    {
                        if (key == null) continue;
                        var id = key.GetValue("NetCfgInstanceId") as string;
                        if (id == null || !activeIds.Any(a => string.Equals(a, id, StringComparison.OrdinalIgnoreCase))) continue;
                        if (!(key.GetValue("Characteristics") is int ch) || (ch & NcfPhysical) == 0) continue;

                        var path = ClassPath + "\\" + sub;
                        Values.Add(new RegValueSpec { Hive = Hive.LocalMachine, Path = path, Name = "PnPCapabilities", Applied = PnPDisablePowerOff });

                        foreach (var kw in PowerKeywords)
                        {
                            if (key.GetValue(kw) is string && HasOffOption(key, kw))
                                Values.Add(new RegValueSpec { Hive = Hive.LocalMachine, Path = path, Name = kw, Kind = RegistryValueKind.String, Applied = "0" });
                        }
                    }
                }
            }
        }

        /// <summary>Sürücünün bu ayar için "0" seçeneği sunduğunu doğrular (Ndi\Params\&lt;ad&gt;\enum).</summary>
        private static bool HasOffOption(RegistryKey adapter, string keyword)
        {
            using (var e = adapter.OpenSubKey($@"Ndi\Params\{keyword}\enum", false))
            {
                return e != null && e.GetValue("0") != null;
            }
        }
    }
}
