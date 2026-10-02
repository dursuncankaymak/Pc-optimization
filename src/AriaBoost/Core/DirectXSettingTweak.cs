using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace AriaBoost.Core
{
    /// <summary>
    /// Ayarlar → Ekran → Grafik → "Varsayılan grafik ayarları" sayfasındaki seçenekler.
    /// Windows bunların hepsini tek bir metin değerinde "Anahtar=Değer;" biçiminde tutar
    /// (ör. "SwapEffectUpgradeEnable=1;VRROptimizeEnable=0;"). Bu sınıf yalnızca kendi
    /// anahtarını değiştirir, diğerlerine dokunmaz.
    /// </summary>
    public sealed class DirectXSettingTweak : Tweak
    {
        public const string RegPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
        public const string RegName = "DirectXUserGlobalSettings";

        public string SettingKey { get; set; }
        public string AppliedValue { get; set; } = "1";
        public string DefaultValue { get; set; } = "0";

        private string BackupKey => "dx:" + SettingKey;

        protected override TweakState ReadState()
        {
            var map = Parse(ReadRaw());
            var value = map.TryGetValue(SettingKey, out var v) ? v : DefaultValue;
            return value == AppliedValue ? TweakState.Applied : TweakState.NotApplied;
        }

        public override void Apply(BackupStore store)
        {
            var map = Parse(ReadRaw());
            store.SaveIfMissing(Id, BackupKey, new BackupEntry
            {
                Exists = map.ContainsKey(SettingKey),
                Kind = "String",
                Value = map.TryGetValue(SettingKey, out var old) ? old : null,
            });
            map[SettingKey] = AppliedValue;
            WriteRaw(Format(map));
        }

        public override void Revert(BackupStore store)
        {
            var map = Parse(ReadRaw());
            var backup = store.Get(Id, BackupKey);
            if (backup != null && backup.Exists) map[SettingKey] = backup.Value;
            else map.Remove(SettingKey);
            WriteRaw(Format(map));
            store.Remove(Id);
        }

        public static string ReadRaw() => Reg.Read(Hive.CurrentUser, RegPath, RegName) as string;

        public static void WriteRaw(string value)
        {
            if (string.IsNullOrEmpty(value)) Reg.Delete(Hive.CurrentUser, RegPath, RegName);
            else Reg.Write(Hive.CurrentUser, RegPath, RegName, value, RegistryValueKind.String);
        }

        /// <summary>Sırayı koruyarak ayrıştırır.</summary>
        public static OrderedMap Parse(string raw)
        {
            var map = new OrderedMap();
            if (string.IsNullOrEmpty(raw)) return map;
            foreach (var part in raw.Split(';'))
            {
                var p = part.Trim();
                if (p.Length == 0) continue;
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;
                map[p.Substring(0, eq)] = p.Substring(eq + 1);
            }
            return map;
        }

        public static string Format(OrderedMap map) => string.Concat(map.Select(kv => kv.Key + "=" + kv.Value + ";"));

        /// <summary>Ekleme sırasını koruyan küçük sözlük.</summary>
        public sealed class OrderedMap : IEnumerable<KeyValuePair<string, string>>
        {
            private readonly List<KeyValuePair<string, string>> items = new List<KeyValuePair<string, string>>();

            public string this[string key]
            {
                set
                {
                    int i = items.FindIndex(kv => kv.Key == key);
                    if (i >= 0) items[i] = new KeyValuePair<string, string>(key, value);
                    else items.Add(new KeyValuePair<string, string>(key, value));
                }
            }

            public bool ContainsKey(string key) => items.Any(kv => kv.Key == key);

            public bool TryGetValue(string key, out string value)
            {
                foreach (var kv in items)
                {
                    if (kv.Key == key) { value = kv.Value; return true; }
                }
                value = null;
                return false;
            }

            public void Remove(string key) => items.RemoveAll(kv => kv.Key == key);

            public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => items.GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
