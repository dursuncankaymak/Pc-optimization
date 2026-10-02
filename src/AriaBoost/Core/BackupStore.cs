using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace AriaBoost.Core
{
    /// <summary>Bir ayarın, değiştirilmeden önceki hâli.</summary>
    [DataContract]
    public sealed class BackupEntry
    {
        [DataMember] public bool Exists { get; set; }
        [DataMember] public string Kind { get; set; }
        [DataMember] public string Value { get; set; }
    }

    /// <summary>
    /// Her optimizasyonun, uygulanmadan önceki orijinal değerlerini saklar.
    /// Böylece "Geri al" bilgisayarı tam olarak önceki hâline döndürür.
    /// Dosya: %LocalAppData%\AriaBoost\backup.json
    /// </summary>
    public sealed class BackupStore
    {
        [DataContract]
        private sealed class Model
        {
            [DataMember] public Dictionary<string, Dictionary<string, BackupEntry>> Tweaks { get; set; }
        }

        private readonly string path;
        private readonly Dictionary<string, Dictionary<string, BackupEntry>> data;
        private readonly object gate = new object();

        public static string DefaultDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AriaBoost");

        public BackupStore(string filePath)
        {
            path = filePath;
            data = Load(filePath);
        }

        public static BackupStore OpenDefault() => new BackupStore(Path.Combine(DefaultDirectory, "backup.json"));

        public bool Has(string tweakId, string key)
        {
            lock (gate) return data.TryGetValue(tweakId, out var d) && d.ContainsKey(key);
        }

        public bool HasAny(string tweakId)
        {
            lock (gate) return data.TryGetValue(tweakId, out var d) && d.Count > 0;
        }

        public IReadOnlyCollection<string> TweakIds
        {
            get { lock (gate) return new List<string>(data.Keys); }
        }

        /// <summary>Yalnızca ilk kez kaydeder: aynı ayar iki kez uygulansa da gerçek orijinal korunur.</summary>
        public void SaveIfMissing(string tweakId, string key, BackupEntry entry)
        {
            lock (gate)
            {
                if (!data.TryGetValue(tweakId, out var d)) data[tweakId] = d = new Dictionary<string, BackupEntry>();
                if (d.ContainsKey(key)) return;
                d[key] = entry;
                Flush();
            }
        }

        public IReadOnlyCollection<string> KeysOf(string tweakId)
        {
            lock (gate) return data.TryGetValue(tweakId, out var d) ? new List<string>(d.Keys) : new List<string>();
        }

        public BackupEntry Get(string tweakId, string key)
        {
            lock (gate) return data.TryGetValue(tweakId, out var d) && d.TryGetValue(key, out var e) ? e : null;
        }

        public void Remove(string tweakId)
        {
            lock (gate)
            {
                if (data.Remove(tweakId)) Flush();
            }
        }

        private void Flush()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            using (var fs = File.Create(tmp))
            {
                Serializer().WriteObject(fs, new Model { Tweaks = data });
            }
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        private static Dictionary<string, Dictionary<string, BackupEntry>> Load(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    using (var fs = File.OpenRead(file))
                    {
                        var model = (Model)Serializer().ReadObject(fs);
                        if (model?.Tweaks != null) return model.Tweaks;
                    }
                }
            }
            catch (Exception)
            {
                // Bozuk yedek dosyası uygulamayı açılmaz hâle getirmesin; kenara alınır.
                try { File.Copy(file, file + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true); } catch { }
            }
            return new Dictionary<string, Dictionary<string, BackupEntry>>();
        }

        private static DataContractJsonSerializer Serializer() =>
            new DataContractJsonSerializer(typeof(Model), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        public static string Describe(BackupEntry e) =>
            e == null ? "(yedek yok)" : e.Exists ? $"{e.Kind}:{e.Value}" : "(yoktu)";

        internal static string EncodeValue(object value)
        {
            switch (value)
            {
                case null: return null;
                case byte[] bytes: return Convert.ToBase64String(bytes);
                case string[] multi: return string.Join("\0", multi);
                default: return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        internal static object DecodeValue(string kind, string value)
        {
            switch (kind)
            {
                case "DWord": return unchecked((int)long.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
                case "QWord": return long.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
                case "Binary": return Convert.FromBase64String(value);
                case "MultiString": return value.Length == 0 ? new string[0] : value.Split('\0');
                default: return value;
            }
        }


    }
}
