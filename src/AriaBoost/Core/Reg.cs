using System;
using Microsoft.Win32;

namespace AriaBoost.Core
{
    public enum Hive { CurrentUser, LocalMachine, Users }

    /// <summary>
    /// Kayıt defteri yardımcıları. 64 bit Windows'ta her zaman 64 bit görünümü açar;
    /// böylece HKLM\SOFTWARE yolları WOW6432Node'a yönlenmez.
    /// </summary>
    public static class Reg
    {
        public static RegistryKey OpenBase(Hive hive)
        {
            var view = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Default;
            switch (hive)
            {
                case Hive.CurrentUser: return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, view);
                case Hive.LocalMachine: return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                case Hive.Users: return RegistryKey.OpenBaseKey(RegistryHive.Users, view);
                default: throw new ArgumentOutOfRangeException(nameof(hive));
            }
        }

        public static string HiveName(Hive hive)
        {
            switch (hive)
            {
                case Hive.CurrentUser: return "HKCU";
                case Hive.LocalMachine: return "HKLM";
                default: return "HKU";
            }
        }

        /// <summary>Değeri okur; anahtar ya da değer yoksa null döner.</summary>
        public static object Read(Hive hive, string path, string name)
        {
            using (var root = OpenBase(hive))
            using (var key = root.OpenSubKey(path, false))
            {
                return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
        }

        public static RegistryValueKind? ReadKind(Hive hive, string path, string name)
        {
            using (var root = OpenBase(hive))
            using (var key = root.OpenSubKey(path, false))
            {
                if (key == null || key.GetValue(name) == null) return null;
                return key.GetValueKind(name);
            }
        }

        public static void Write(Hive hive, string path, string name, object value, RegistryValueKind kind)
        {
            using (var root = OpenBase(hive))
            using (var key = root.CreateSubKey(path, true))
            {
                if (key == null) throw new InvalidOperationException($"Kayıt anahtarı açılamadı: {HiveName(hive)}\\{path}");
                key.SetValue(name, value, kind);
            }
        }

        public static void Delete(Hive hive, string path, string name)
        {
            using (var root = OpenBase(hive))
            using (var key = root.OpenSubKey(path, true))
            {
                key?.DeleteValue(name, false);
            }
        }

        public static bool ValuesEqual(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is byte[] ba && b is byte[] bb)
            {
                if (ba.Length != bb.Length) return false;
                for (int i = 0; i < ba.Length; i++) if (ba[i] != bb[i]) return false;
                return true;
            }
            if (a is string[] sa && b is string[] sb) return string.Join("\0", sa) == string.Join("\0", sb);
            if (IsNumber(a) && IsNumber(b)) return Convert.ToInt64(a) == Convert.ToInt64(b);
            return string.Equals(Convert.ToString(a), Convert.ToString(b), StringComparison.Ordinal);
        }

        private static bool IsNumber(object o) => o is int || o is long || o is uint || o is ulong;
    }
}
