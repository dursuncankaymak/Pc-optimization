using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace AriaBoost.Core
{
    /// <summary>
    /// Uygulamadaki tüm optimizasyonlar.
    ///
    /// Seçim ilkesi: yalnızca etkisi bilinen, güvenliği azaltmayan ve birebir geri
    /// alınabilen ayarlar. Bilerek EKLENMEYENLER: Windows Defender'ı / güncellemeleri /
    /// Spectre-Meltdown korumalarını / Bellek Bütünlüğü'nü kapatmak, hizmet devre dışı
    /// bırakmak, "RAM temizleyici", Nagle/HPET/bcdedit gibi etkisi kanıtlanmamış ayarlar.
    /// </summary>
    public static class TweakCatalog
    {
        public static IReadOnlyList<Tweak> Create()
        {
            bool laptop = SafeHasBattery();

            return new List<Tweak>
            {
                // ---------------- Oyun ----------------
                new RegistryTweak
                {
                    Id = "game-mode",
                    Category = Categories.Gaming,
                    Title = "Oyun Modu",
                    Description = "Oyun açıkken Windows Update'in sürücü kurmasını ve yeniden başlatma bildirimlerini durdurur ve daha kararlı bir kare hızı sağlamaya yardım eder. Windows'ta varsayılan olarak açıktır.",
                    Impact = Impact.Medium,
                    Recommended = true,
                    Values =
                    {
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"Software\Microsoft\GameBar", Name = "AutoGameModeEnabled", Applied = 1, DefaultWhenMissing = 1 },
                    },
                },
                new RegistryTweak
                {
                    Id = "game-dvr",
                    Category = Categories.Gaming,
                    Title = "Arka plan oyun kaydını kapat (Game DVR)",
                    Description = "Xbox Game Bar'ın oyunları arka planda kaydetmeye hazır beklemesini kapatır. Kayıt alt sistemi FPS'i ve kare sürelerini olumsuz etkileyebilir. Game Bar açılmaya devam eder; yalnızca ekran kaydı kapanır.",
                    Impact = Impact.High,
                    Recommended = true,
                    Values =
                    {
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"System\GameConfigStore", Name = "GameDVR_Enabled", Applied = 0, DefaultWhenMissing = 1 },
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"Software\Microsoft\Windows\CurrentVersion\GameDVR", Name = "AppCaptureEnabled", Applied = 0, DefaultWhenMissing = 1 },
                    },
                },
                new RegistryTweak
                {
                    Id = "hags",
                    Category = Categories.Gaming,
                    Title = "Donanım hızlandırmalı GPU zamanlaması",
                    Description = "Ekran kartının kendi belleğini kendisi yönetmesini sağlar; işlemci yükünü ve gecikmeyi azaltabilir. NVIDIA DLSS Kare Oluşturma için gereklidir. GTX 1000 / RX 5000 / Intel Arc ve üstü kartlarda çalışır, eski kartlarda etkisizdir.",
                    Impact = Impact.Medium,
                    Recommended = true,
                    RequiresRestart = true,
                    MinBuild = 19041,
                    Values =
                    {
                        new RegValueSpec { Hive = Hive.LocalMachine, Path = @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", Name = "HwSchMode", Applied = 2 },
                    },
                },
                new DirectXSettingTweak
                {
                    Id = "windowed-optimizations",
                    Category = Categories.Gaming,
                    Title = "Pencereli oyunlar için iyileştirmeler",
                    Description = "Pencereli ve kenarlıksız pencere modundaki DirectX 10/11 oyunlarında gecikmeyi azaltır, bu oyunlarda Otomatik HDR ve değişken yenileme hızını (VRR) kullanılabilir hâle getirir.",
                    Impact = Impact.Medium,
                    Recommended = true,
                    MinBuild = OsInfo.Windows11Build,
                    SettingKey = "SwapEffectUpgradeEnable",
                },
                new DirectXSettingTweak
                {
                    Id = "vrr-optimize",
                    Category = Categories.Gaming,
                    Title = "Değişken yenileme hızı (VRR) desteği",
                    Description = "G-Sync / FreeSync monitörlerde, VRR'yi kendisi desteklemeyen DirectX 11 oyunlarında da değişken yenileme hızını açar. VRR'siz monitörlerde etkisizdir.",
                    Impact = Impact.Low,
                    SettingKey = "VRROptimizeEnable",
                },
                new RegistryTweak
                {
                    Id = "gamebar-controller",
                    Category = Categories.Gaming,
                    Title = "Kumandadaki Xbox tuşu Game Bar'ı açmasın",
                    Description = "Oyun sırasında kumandanın Xbox tuşuna basınca Game Bar'ın açılıp oyunu bölmesini engeller.",
                    Impact = Impact.Low,
                    Values =
                    {
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"Software\Microsoft\GameBar", Name = "UseNexusForGameBarEnabled", Applied = 0, DefaultWhenMissing = 1 },
                    },
                },

                // ---------------- Sistem ----------------
                new PowerPlanTweak
                {
                    Id = "power-plan",
                    Category = Categories.System,
                    Title = "Nihai Performans güç planı",
                    Description = laptop
                        ? "İşlemcinin güç tasarrufu için frekans düşürmesini ve çekirdekleri uyutmasını engeller; ani FPS düşüşlerini azaltır. Dizüstü bilgisayarda pil süresini belirgin kısaltır ve ısıyı artırır; yalnızca prize takılıyken önerilir."
                        : "İşlemcinin güç tasarrufu için frekans düşürmesini ve çekirdekleri uyutmasını engeller; ani FPS düşüşlerini ve takılmaları azaltır. Boşta güç tüketimi biraz artar.",
                    Impact = Impact.High,
                    Recommended = !laptop,
                },
                new RegistryTweak
                {
                    Id = "transparency",
                    Category = Categories.System,
                    Title = "Saydamlık efektlerini kapat",
                    Description = "Başlat menüsü, görev çubuğu ve pencerelerdeki buzlu cam efektini kapatır. Zayıf ekran kartlarında masaüstünün harcadığı GPU gücünü azaltır.",
                    Impact = Impact.Low,
                    Values =
                    {
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", Name = "EnableTransparency", Applied = 0, DefaultWhenMissing = 1 },
                    },
                },

                // ---------------- Fare ve klavye ----------------
                new RegistryTweak
                {
                    Id = "mouse-acceleration",
                    Category = Categories.Input,
                    Title = "Fare ivmesini kapat",
                    Description = "\"İşaretçi hassasiyetini artır\" ayarını kapatır. Fareyi aynı mesafe kaydırdığında nişangah her zaman aynı mesafe gider; nişan almayı kas hafızasına bağlar.",
                    Impact = Impact.High,
                    Recommended = true,
                    Values =
                    {
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"Control Panel\Mouse", Name = "MouseSpeed", Kind = RegistryValueKind.String, Applied = "0", DefaultWhenMissing = "1" },
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"Control Panel\Mouse", Name = "MouseThreshold1", Kind = RegistryValueKind.String, Applied = "0", DefaultWhenMissing = "6" },
                        new RegValueSpec { Hive = Hive.CurrentUser, Path = @"Control Panel\Mouse", Name = "MouseThreshold2", Kind = RegistryValueKind.String, Applied = "0", DefaultWhenMissing = "10" },
                    },
                    AfterChange = ApplyMouseSettingsLive,
                },
                new AccessibilityShortcutsTweak
                {
                    Id = "accessibility-shortcuts",
                    Category = Categories.Input,
                    Title = "Yapışkan Tuşlar kısayolunu kapat",
                    Description = "5 kez Shift'e basınca (veya sağ Shift'i 8 sn, NumLock'u 5 sn basılı tutunca) çıkan ve oyunu masaüstüne atan pencereyi kapatır. Özelliklerin kendisi Ayarlar'dan kullanılabilir kalır.",
                    Impact = Impact.Medium,
                    Recommended = true,
                },

                // ---------------- Ağ ----------------
                new NetworkAdapterPowerTweak
                {
                    Id = "nic-power-saving",
                    Category = Categories.Network,
                    Title = "Ağ kartı güç tasarrufunu kapat",
                    Description = "Ağ kartının \"Enerji Verimli Ethernet\" / \"Green Ethernet\" gibi tasarruf özelliklerini ve Windows'un kartı uyutmasını kapatır. Bu özellikler hat boşken kartı uyutur; uyanırken ping sıçramalarına, bazı kartlarda (özellikle Realtek) kopmalara yol açabilir. Yalnızca kartının desteklediği ayarlar değiştirilir.",
                    Impact = Impact.Medium,
                    Recommended = true,
                    RequiresRestart = true,
                },
                new RegistryTweak
                {
                    Id = "delivery-optimization",
                    Category = Categories.Network,
                    Title = "Güncellemeleri başka bilgisayarlara yüklemeyi kapat",
                    Description = "Windows Update'in indirdiği güncellemeleri internetteki ve ağındaki başka bilgisayarlara göndermesini (Teslim İyileştirme) kapatır. Oyun sırasında yükleme bant genişliğinin ve ping'in boşa gitmesini önler. Güncellemeler normal şekilde inmeye devam eder.",
                    Impact = Impact.Medium,
                    Recommended = true,
                    Values =
                    {
                        new RegValueSpec { Hive = Hive.Users, Path = @"S-1-5-20\Software\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Settings", Name = "DownloadMode", Applied = 0, DefaultWhenMissing = 1 },
                    },
                },
            };
        }

        /// <summary>Kayıt defterindeki fare değerlerini oturumu kapatmadan anında uygular.</summary>
        private static void ApplyMouseSettingsLive()
        {
            const string path = @"Control Panel\Mouse";
            int Read(string name, int fallback) =>
                int.TryParse(Convert.ToString(Reg.Read(Hive.CurrentUser, path, name)), out var v) ? v : fallback;

            var values = new[] { Read("MouseThreshold1", 6), Read("MouseThreshold2", 10), Read("MouseSpeed", 1) };
            Native.SystemParametersInfo(Native.SPI_SETMOUSE, 0, values, Native.SPIF_SENDCHANGE);
        }

        private static bool SafeHasBattery()
        {
            try { return PowerPlans.HasBattery(); }
            catch { return false; }
        }
    }
}
