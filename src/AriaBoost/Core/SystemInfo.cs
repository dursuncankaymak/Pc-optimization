using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;

namespace AriaBoost.Core
{
    /// <summary>Genel Bakış sayfasında gösterilen donanım ve durum bilgisi.</summary>
    public sealed class SystemInfo
    {
        public OsInfo Os { get; private set; }
        public string Cpu { get; private set; }
        public int LogicalCores { get; private set; }
        public List<string> Gpus { get; private set; } = new List<string>();
        public ulong TotalRam { get; private set; }
        public ulong AvailableRam { get; private set; }
        public long SystemDriveFree { get; private set; }
        public long SystemDriveTotal { get; private set; }
        public string PowerPlan { get; private set; }
        public bool HasBattery { get; private set; }

        /// <summary>Bellek Bütünlüğü (HVCI). Güvenlik özelliği olduğu için yalnızca bilgi verilir, değiştirilmez.</summary>
        public bool? MemoryIntegrity { get; private set; }

        public static SystemInfo Collect()
        {
            var info = new SystemInfo { Os = OsInfo.Current, LogicalCores = Environment.ProcessorCount };

            info.Cpu = Try(() => Convert.ToString(Reg.Read(Hive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString"))?.Trim()) ?? "Bilinmiyor";

            info.Gpus = Try(() =>
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    return searcher.Get().Cast<ManagementObject>()
                        .Select(o => Convert.ToString(o["Name"]))
                        .Where(n => !string.IsNullOrWhiteSpace(n) && n.IndexOf("Basic Display", StringComparison.OrdinalIgnoreCase) < 0
                                    && n.IndexOf("Remote", StringComparison.OrdinalIgnoreCase) < 0)
                        .Distinct()
                        .ToList();
                }
            }) ?? new List<string>();

            var mem = new Native.MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX)) };
            if (Native.GlobalMemoryStatusEx(ref mem))
            {
                info.TotalRam = mem.ullTotalPhys;
                info.AvailableRam = mem.ullAvailPhys;
            }

            Try(() =>
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)));
                info.SystemDriveFree = drive.AvailableFreeSpace;
                info.SystemDriveTotal = drive.TotalSize;
                return true;
            });

            info.PowerPlan = Try(() => PowerPlans.GetName(PowerPlans.GetActive())) ?? "Bilinmiyor";
            info.HasBattery = Try(() => PowerPlans.HasBattery());

            var hvci = Reg.Read(Hive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
            info.MemoryIntegrity = hvci is int v ? v == 1 : (bool?)false;

            return info;
        }

        private static T Try<T>(Func<T> f)
        {
            try { return f(); }
            catch { return default(T); }
        }

        public static string FormatBytes(double bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            while (bytes >= 1024 && i < units.Length - 1) { bytes /= 1024; i++; }
            return i == 0 ? $"{bytes:0} {units[i]}" : $"{bytes:0.#} {units[i]}";
        }
    }
}
