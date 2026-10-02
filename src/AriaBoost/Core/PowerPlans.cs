using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace AriaBoost.Core
{
    /// <summary>Windows güç planlarını powrprof.dll API'si ile yönetir (powercfg çıktısı ayrıştırılmaz).</summary>
    public static class PowerPlans
    {
        public static readonly Guid Balanced = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
        public static readonly Guid HighPerformance = new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        public static readonly Guid UltimatePerformance = new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        public static Guid GetActive()
        {
            uint err = Native.PowerGetActiveScheme(IntPtr.Zero, out var ptr);
            if (err != 0) throw new TweakException($"Etkin güç planı okunamadı (hata {err}).");
            try { return (Guid)Marshal.PtrToStructure(ptr, typeof(Guid)); }
            finally { Native.LocalFree(ptr); }
        }

        public static void SetActive(Guid scheme)
        {
            uint err = Native.PowerSetActiveScheme(IntPtr.Zero, ref scheme);
            if (err != 0) throw new TweakException($"Güç planı etkinleştirilemedi (hata {err}).");
        }

        public static List<Guid> List()
        {
            var result = new List<Guid>();
            for (uint i = 0; ; i++)
            {
                var buffer = new byte[16];
                uint size = (uint)buffer.Length;
                uint err = Native.PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, Native.ACCESS_SCHEME, i, buffer, ref size);
                if (err != 0) break; // ERROR_NO_MORE_ITEMS
                result.Add(new Guid(buffer));
            }
            return result;
        }

        public static bool Exists(Guid scheme) => List().Contains(scheme);

        public static string GetName(Guid scheme)
        {
            uint size = 0;
            Native.PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, null, ref size);
            if (size == 0) return null;
            var buffer = new byte[size];
            if (Native.PowerReadFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, buffer, ref size) != 0) return null;
            return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
        }

        public static void SetName(Guid scheme, string name)
        {
            var buffer = Encoding.Unicode.GetBytes(name + "\0");
            Native.PowerWriteFriendlyName(IntPtr.Zero, ref scheme, IntPtr.Zero, IntPtr.Zero, buffer, (uint)buffer.Length);
        }

        /// <summary>Bir planı kopyalar; Nihai Performans gibi gizli planlar da kopyalanabilir.</summary>
        public static Guid? Duplicate(Guid source)
        {
            IntPtr dest = IntPtr.Zero;
            uint err = Native.PowerDuplicateScheme(IntPtr.Zero, ref source, ref dest);
            if (err != 0 || dest == IntPtr.Zero) return null;
            try { return (Guid)Marshal.PtrToStructure(dest, typeof(Guid)); }
            finally { Native.LocalFree(dest); }
        }

        public static void Delete(Guid scheme)
        {
            Native.PowerDeleteScheme(IntPtr.Zero, ref scheme);
        }

        /// <summary>Pil var mı (dizüstü bilgisayar)?</summary>
        public static bool HasBattery()
        {
            if (!Native.GetSystemPowerStatus(out var status)) return false;
            // 128 = sistem pili yok, 255 = bilinmiyor
            return status.BatteryFlag != 128 && status.BatteryFlag != 255;
        }
    }

    /// <summary>
    /// Nihai Performans (yoksa Yüksek Performans) güç planına geçer. İşlemcinin
    /// güç tasarrufu için frekans düşürmesini ve çekirdek uyutmasını azaltır.
    /// </summary>
    public sealed class PowerPlanTweak : Tweak
    {
        public const string PlanName = "Aria Boost - Nihai Performans";
        private const string PreviousKey = "previous-scheme";
        private const string CreatedKey = "created-scheme";

        protected override TweakState ReadState()
        {
            var active = PowerPlans.GetActive();
            if (active == PowerPlans.HighPerformance || active == PowerPlans.UltimatePerformance) return TweakState.Applied;
            return PowerPlans.GetName(active) == PlanName ? TweakState.Applied : TweakState.NotApplied;
        }

        public override void Apply(BackupStore store)
        {
            var previous = PowerPlans.GetActive();
            store.SaveIfMissing(Id, PreviousKey, new BackupEntry { Exists = true, Kind = "Guid", Value = previous.ToString() });

            var schemes = PowerPlans.List();

            // Daha önce oluşturduğumuz plan duruyorsa yeniden kopyalamak yerine onu kullan.
            foreach (var g in schemes)
            {
                if (PowerPlans.GetName(g) == PlanName && TryActivate(g)) return;
            }

            var created = PowerPlans.Duplicate(PowerPlans.UltimatePerformance);
            if (created.HasValue)
            {
                PowerPlans.SetName(created.Value, PlanName);
                if (TryActivate(created.Value))
                {
                    store.SaveIfMissing(Id, CreatedKey, new BackupEntry { Exists = true, Kind = "Guid", Value = created.Value.ToString() });
                    return;
                }
                PowerPlans.Delete(created.Value);
            }

            // Nihai Performans kullanılamıyorsa (bazı dizüstülerde) Yüksek Performans'a düş.
            if (schemes.Contains(PowerPlans.HighPerformance) && TryActivate(PowerPlans.HighPerformance)) return;

            store.Remove(Id);
            throw new TweakException(
                "Bu bilgisayar yüksek performans güç planlarını desteklemiyor. " +
                "Modern Bekleme (Modern Standby) kullanan bazı dizüstülerde yalnızca Dengeli plan bulunur; " +
                "bunun yerine Ayarlar → Sistem → Güç bölümünden \"En iyi performans\" modunu seçebilirsin.");
        }

        public override void Revert(BackupStore store)
        {
            var previousEntry = store.Get(Id, PreviousKey);
            var target = PowerPlans.Balanced;
            if (previousEntry != null && Guid.TryParse(previousEntry.Value, out var prev) && PowerPlans.Exists(prev)) target = prev;

            // Geri dönülecek plan bizim planımızsa (eski bir yedekten) Dengeli'ye dön.
            if (PowerPlans.GetName(target) == PlanName) target = PowerPlans.Balanced;
            PowerPlans.SetActive(target);

            // Bizim oluşturduğumuz planları temizle.
            foreach (var g in PowerPlans.List())
            {
                if (g != target && PowerPlans.GetName(g) == PlanName) PowerPlans.Delete(g);
            }
            store.Remove(Id);
        }

        private static bool TryActivate(Guid scheme)
        {
            if (Native.PowerSetActiveScheme(IntPtr.Zero, ref scheme) != 0) return false;
            return PowerPlans.GetActive() == scheme;
        }
    }
}
