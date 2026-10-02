using System.Runtime.InteropServices;

namespace AriaBoost.Core
{
    /// <summary>
    /// Oyun sırasında yanlışlıkla tetiklenip oyunu masaüstüne atan erişilebilirlik
    /// kısayollarını kapatır: 5 kez Shift (Yapışkan Tuşlar), 8 sn sağ Shift (Filtre Tuşları),
    /// 5 sn NumLock (Geçiş Tuşları). Özelliklerin kendisi kapatılmaz, yalnızca kısayolları.
    /// SystemParametersInfo ile ayar hem anında uygulanır hem kalıcı kaydedilir.
    /// </summary>
    public sealed class AccessibilityShortcutsTweak : Tweak
    {
        private const uint HotkeyActive = 0x4; // SKF_/FKF_/TKF_HOTKEYACTIVE
        private const uint Persist = Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE;

        protected override TweakState ReadState()
        {
            int off = 0;
            if ((GetSticky().dwFlags & HotkeyActive) == 0) off++;
            if ((GetFilter().dwFlags & HotkeyActive) == 0) off++;
            if ((GetToggle().dwFlags & HotkeyActive) == 0) off++;
            return off == 3 ? TweakState.Applied : off == 0 ? TweakState.NotApplied : TweakState.Partial;
        }

        public override void Apply(BackupStore store)
        {
            var sticky = GetSticky();
            var filter = GetFilter();
            var toggle = GetToggle();
            Save(store, "sticky", sticky.dwFlags);
            Save(store, "filter", filter.dwFlags);
            Save(store, "toggle", toggle.dwFlags);

            sticky.dwFlags &= ~HotkeyActive;
            filter.dwFlags &= ~HotkeyActive;
            toggle.dwFlags &= ~HotkeyActive;
            Set(sticky, filter, toggle);
        }

        public override void Revert(BackupStore store)
        {
            var sticky = GetSticky();
            var filter = GetFilter();
            var toggle = GetToggle();
            sticky.dwFlags = Restore(store, "sticky", sticky.dwFlags);
            filter.dwFlags = Restore(store, "filter", filter.dwFlags);
            toggle.dwFlags = Restore(store, "toggle", toggle.dwFlags);
            Set(sticky, filter, toggle);
            store.Remove(Id);
        }

        /// <summary>Test için: üç ayarın ham bayrakları.</summary>
        internal static string Snapshot() => $"{GetSticky().dwFlags}/{GetFilter().dwFlags}/{GetToggle().dwFlags}";

        private void Save(BackupStore store, string key, uint flags) =>
            store.SaveIfMissing(Id, key, new BackupEntry { Exists = true, Kind = "Flags", Value = flags.ToString() });

        private uint Restore(BackupStore store, string key, uint current)
        {
            var e = store.Get(Id, key);
            if (e != null && uint.TryParse(e.Value, out var flags)) return flags;
            return current | HotkeyActive; // Windows varsayılanı: kısayol açık
        }

        private static void Set(Native.STICKYKEYS sticky, Native.FILTERKEYS filter, Native.TOGGLEKEYS toggle)
        {
            if (!Native.SystemParametersInfo(Native.SPI_SETSTICKYKEYS, sticky.cbSize, ref sticky, Persist) ||
                !Native.SystemParametersInfo(Native.SPI_SETFILTERKEYS, filter.cbSize, ref filter, Persist) ||
                !Native.SystemParametersInfo(Native.SPI_SETTOGGLEKEYS, toggle.cbSize, ref toggle, Persist))
            {
                throw new TweakException($"Erişilebilirlik ayarı yazılamadı (hata {Marshal.GetLastWin32Error()}).");
            }
        }

        private static Native.STICKYKEYS GetSticky()
        {
            var s = new Native.STICKYKEYS { cbSize = (uint)Marshal.SizeOf(typeof(Native.STICKYKEYS)) };
            Native.SystemParametersInfo(Native.SPI_GETSTICKYKEYS, s.cbSize, ref s, 0);
            return s;
        }

        private static Native.FILTERKEYS GetFilter()
        {
            var f = new Native.FILTERKEYS { cbSize = (uint)Marshal.SizeOf(typeof(Native.FILTERKEYS)) };
            Native.SystemParametersInfo(Native.SPI_GETFILTERKEYS, f.cbSize, ref f, 0);
            return f;
        }

        private static Native.TOGGLEKEYS GetToggle()
        {
            var t = new Native.TOGGLEKEYS { cbSize = (uint)Marshal.SizeOf(typeof(Native.TOGGLEKEYS)) };
            Native.SystemParametersInfo(Native.SPI_GETTOGGLEKEYS, t.cbSize, ref t, 0);
            return t;
        }
    }
}
