using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;

namespace AriaBoost.Core
{
    public enum StartupSource { UserRun, MachineRun, MachineRun32, UserFolder, CommonFolder }

    public sealed class StartupItem
    {
        public string Name { get; set; }
        public string Command { get; set; }
        public StartupSource Source { get; set; }
        public bool Enabled { get; set; }

        /// <summary>Komuttan çıkarılan çalıştırılabilir dosya yolu (simge için).</summary>
        public string ExecutablePath { get; set; }

        public string SourceLabel
        {
            get
            {
                switch (Source)
                {
                    case StartupSource.UserRun:
                    case StartupSource.UserFolder: return "Bu kullanıcı";
                    default: return "Tüm kullanıcılar";
                }
            }
        }
    }

    /// <summary>
    /// Açılışta başlayan programlar. Açma/kapama, Görev Yöneticisi'nin "Başlangıç"
    /// sekmesinin kullandığı StartupApproved kayıtlarıyla yapılır: program silinmez,
    /// yalnızca açılışta başlaması engellenir ve Görev Yöneticisi'nden de geri açılabilir.
    /// </summary>
    public static class StartupManager
    {
        private const string RunPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string Run32Path = @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

        public static List<StartupItem> List()
        {
            var items = new List<StartupItem>();
            AddRunKey(items, Hive.CurrentUser, RunPath, StartupSource.UserRun);
            AddRunKey(items, Hive.LocalMachine, RunPath, StartupSource.MachineRun);
            if (Environment.Is64BitOperatingSystem) AddRunKey(items, Hive.LocalMachine, Run32Path, StartupSource.MachineRun32);
            AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.Startup), StartupSource.UserFolder);
            AddFolder(items, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), StartupSource.CommonFolder);
            return items.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static void SetEnabled(StartupItem item, bool enabled)
        {
            GetApprovedLocation(item.Source, out var hive, out var path);
            var data = new byte[12];
            if (enabled)
            {
                data[0] = 0x02;
            }
            else
            {
                data[0] = 0x03;
                BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            }
            Reg.Write(hive, path, item.Name, data, RegistryValueKind.Binary);
            item.Enabled = enabled;
        }

        private static void AddRunKey(List<StartupItem> items, Hive hive, string path, StartupSource source)
        {
            using (var root = Reg.OpenBase(hive))
            using (var key = root.OpenSubKey(path, false))
            {
                if (key == null) return;
                foreach (var name in key.GetValueNames())
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    var command = Convert.ToString(key.GetValue(name, "", RegistryValueOptions.None));
                    if (string.IsNullOrWhiteSpace(command)) continue;
                    items.Add(new StartupItem
                    {
                        Name = name,
                        Command = command,
                        Source = source,
                        Enabled = IsApproved(source, name),
                        ExecutablePath = ExtractExecutable(command),
                    });
                }
            }
        }

        private static void AddFolder(List<StartupItem> items, string folder, StartupSource source)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                items.Add(new StartupItem
                {
                    Name = fileName,
                    Command = file,
                    Source = source,
                    Enabled = IsApproved(source, fileName),
                    ExecutablePath = file,
                });
            }
        }

        private static bool IsApproved(StartupSource source, string name)
        {
            GetApprovedLocation(source, out var hive, out var path);
            // Kayıt yoksa ya da ilk baytın en düşük biti 0 ise (02, 06) etkindir; 03, 07 kapalı.
            return !(Reg.Read(hive, path, name) is byte[] data) || data.Length == 0 || (data[0] & 1) == 0;
        }

        private static void GetApprovedLocation(StartupSource source, out Hive hive, out string path)
        {
            switch (source)
            {
                case StartupSource.UserRun: hive = Hive.CurrentUser; path = ApprovedRoot + "Run"; break;
                case StartupSource.MachineRun: hive = Hive.LocalMachine; path = ApprovedRoot + "Run"; break;
                case StartupSource.MachineRun32: hive = Hive.LocalMachine; path = ApprovedRoot + "Run32"; break;
                case StartupSource.UserFolder: hive = Hive.CurrentUser; path = ApprovedRoot + "StartupFolder"; break;
                default: hive = Hive.LocalMachine; path = ApprovedRoot + "StartupFolder"; break;
            }
        }

        /// <summary>"\"C:\x\a.exe\" --min" veya "C:\x\a.exe -b" gibi bir komuttan exe yolunu çıkarır.</summary>
        public static string ExtractExecutable(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            var c = Environment.ExpandEnvironmentVariables(command.Trim());
            if (c.StartsWith("\""))
            {
                int end = c.IndexOf('"', 1);
                return end > 1 ? c.Substring(1, end - 1) : null;
            }
            int exe = c.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe > 0) return c.Substring(0, exe + 4);
            int space = c.IndexOf(' ');
            return space > 0 ? c.Substring(0, space) : c;
        }
    }
}
