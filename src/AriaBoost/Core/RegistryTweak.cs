using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace AriaBoost.Core
{
    /// <summary>Bir kayıt defteri değerinin "uygulanmış" hâli.</summary>
    public sealed class RegValueSpec
    {
        public Hive Hive { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public RegistryValueKind Kind { get; set; } = RegistryValueKind.DWord;
        public object Applied { get; set; }

        /// <summary>
        /// Değer hiç yokken Windows'un davrandığı değer. Durum hesaplanırken eksik değer
        /// bununla eşit sayılır; yedek olmadan geri alınırken de bu yazılır.
        /// null ise eksik değer "uygulanmamış" sayılır ve geri alırken değer silinir.
        /// </summary>
        public object DefaultWhenMissing { get; set; }

        public string Key => $"{Reg.HiveName(Hive)}\\{Path}\\{Name}";

        public object ReadEffective() => Reg.Read(Hive, Path, Name) ?? DefaultWhenMissing;

        public bool IsApplied() => Reg.ValuesEqual(ReadEffective(), Applied);
    }

    /// <summary>Bir ya da birkaç kayıt defteri değerini değiştiren optimizasyon.</summary>
    public class RegistryTweak : Tweak
    {
        public List<RegValueSpec> Values { get; } = new List<RegValueSpec>();

        /// <summary>Değerler yazıldıktan sonra (uygula ya da geri al) çalışır; ör. ayarı anında etkinleştirmek için.</summary>
        public Action AfterChange { get; set; }

        protected override TweakState ReadState()
        {
            int applied = Values.Count(v => v.IsApplied());
            if (applied == Values.Count) return TweakState.Applied;
            return applied == 0 ? TweakState.NotApplied : TweakState.Partial;
        }

        public override void Apply(BackupStore store)
        {
            foreach (var v in Values)
            {
                var kind = Reg.ReadKind(v.Hive, v.Path, v.Name);
                var raw = Reg.Read(v.Hive, v.Path, v.Name);
                store.SaveIfMissing(Id, v.Key, new BackupEntry
                {
                    Exists = raw != null,
                    Kind = kind?.ToString(),
                    Value = BackupStore.EncodeValue(raw),
                });
            }
            try
            {
                foreach (var v in Values) Reg.Write(v.Hive, v.Path, v.Name, v.Applied, v.Kind);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException || ex is System.Security.SecurityException)
            {
                throw new TweakException("Kayıt defterine yazma izni yok. Programı yönetici olarak çalıştır.", ex);
            }
            AfterChange?.Invoke();
        }

        public override void Revert(BackupStore store)
        {
            foreach (var v in Values)
            {
                var backup = store.Get(Id, v.Key);
                if (backup != null)
                {
                    if (backup.Exists)
                    {
                        var kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), backup.Kind);
                        Reg.Write(v.Hive, v.Path, v.Name, BackupStore.DecodeValue(backup.Kind, backup.Value), kind);
                    }
                    else
                    {
                        Reg.Delete(v.Hive, v.Path, v.Name);
                    }
                }
                else if (v.DefaultWhenMissing != null)
                {
                    Reg.Write(v.Hive, v.Path, v.Name, v.DefaultWhenMissing, v.Kind);
                }
                else
                {
                    Reg.Delete(v.Hive, v.Path, v.Name);
                }
            }
            store.Remove(Id);
            AfterChange?.Invoke();
        }
    }
}
