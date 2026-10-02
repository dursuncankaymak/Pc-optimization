using System;
using System.Diagnostics;

namespace AriaBoost.Core
{
    /// <summary>
    /// Windows, Wi-Fi'a bağlıyken de yaklaşık dakikada bir çevredeki ağları tarar; tarama
    /// sırasında kart kısa süre kanal değiştirdiği için ping sıçrar. "netsh wlan set autoconfig
    /// enabled=no" taramayı durdurur, mevcut bağlantı sürer. Yan etkisi: bağlantı koparsa Windows
    /// otomatik yeniden bağlanmaz. Bu yüzden yalnızca oyun oturumu boyunca açık tutulur ve
    /// uygulama kapanırken (ya da çökmüşse bir sonraki açılışta) mutlaka geri açılır.
    /// </summary>
    public static class WifiScan
    {
        public const string BackupId = "wifi-scan-session";

        public static void Stop(string interfaceName, BackupStore store)
        {
            store.SaveIfMissing(BackupId, interfaceName, new BackupEntry { Exists = true, Kind = "Interface", Value = interfaceName });
            RunNetsh($"wlan set autoconfig enabled=no interface=\"{interfaceName}\"");
        }

        public static void Resume(string interfaceName, BackupStore store)
        {
            RunNetsh($"wlan set autoconfig enabled=yes interface=\"{interfaceName}\"");
            store.Remove(BackupId);
        }

        public static bool IsStopped(BackupStore store) => store.HasAny(BackupId);

        /// <summary>Önceki oturumdan kalmış (ör. uygulama çöktü) durdurmaları geri açar.</summary>
        public static void RestoreAll(BackupStore store)
        {
            if (!store.HasAny(BackupId)) return;
            foreach (var name in store.KeysOf(BackupId))
            {
                try { RunNetsh($"wlan set autoconfig enabled=yes interface=\"{name}\""); }
                catch (Exception) { }
            }
            store.Remove(BackupId);
        }

        private static void RunNetsh(string args)
        {
            var psi = new ProcessStartInfo("netsh.exe", args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using (var p = Process.Start(psi))
            {
                var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(10000);
                if (p.ExitCode != 0) throw new TweakException("Wi-Fi ayarı değiştirilemedi: " + output.Trim());
            }
        }
    }
}
