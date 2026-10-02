using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using AriaBoost.Core;
using Microsoft.Win32;

namespace AriaBoost.SelfTest
{
    /// <summary>
    /// Gerçek Windows'ta çalışan uçtan uca test. Her optimizasyonu uygular, durumunu
    /// doğrular, ikinci kez uygular (yedeğin ezilmediğini görmek için), geri alır ve
    /// sistemin bit bit ilk hâline döndüğünü kontrol eder.
    /// </summary>
    internal static class Program
    {
        private static int failures;

        [STAThread]
        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            var os = OsInfo.Current;
            Section("Sistem");
            Console.WriteLine($"{os.FullName}  derleme {os.BuildLabel}  (Windows 11: {os.IsWindows11})");
            var info = SystemInfo.Collect();
            Console.WriteLine($"CPU: {info.Cpu} ({info.LogicalCores} iş parçacığı)");
            Console.WriteLine($"GPU: {string.Join(", ", info.Gpus)}");
            Console.WriteLine($"RAM: {SystemInfo.FormatBytes(info.TotalRam)}  Güç planı: {info.PowerPlan}  Pil: {info.HasBattery}  HVCI: {info.MemoryIntegrity}");

            Run("Birim: komuttan exe yolu", UnitExtractExecutable);
            Run("Birim: DirectX ayar metni", UnitDirectXParse);
            Run("Birim: yedek deposu", UnitBackupStore);
            Run("Tema XAML'ı yükleniyor", ThemeLoads);

            var storePath = Path.Combine(Path.GetTempPath(), "ariaboost-selftest-" + Guid.NewGuid().ToString("N") + ".json");
            var store = new BackupStore(storePath);
            foreach (var tweak in TweakCatalog.Create())
            {
                var reason = tweak.UnsupportedReason(os);
                if (reason != null)
                {
                    Console.WriteLine($"[ATLA] {tweak.Id}: {reason}");
                    continue;
                }
                Run($"Uygula → geri al: {tweak.Id}", () => RoundTrip(tweak, store, os));
            }

            Run("DirectX: diğer anahtarlar korunuyor", () => DirectXPreservesOtherKeys(store));
            Run("Başlangıç: aç / kapat", StartupToggle);
            Run("Temizlik: yalnızca eski dosyalar, bağlantı noktalarına girilmez", CleanerSandbox);
            Run("Temizlik: tarama hatasız", () =>
            {
                foreach (var t in Cleaner.CreateTargets())
                {
                    var r = Cleaner.Analyze(t);
                    Console.WriteLine($"    {t.Title}: {r.Files} dosya, {SystemInfo.FormatBytes(r.Bytes)}");
                }
            });

            Section("Sonuç");
            if (failures == 0) Console.WriteLine("TÜM TESTLER GEÇTİ");
            else Console.WriteLine($"{failures} TEST BAŞARISIZ");
            return failures == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------

        private static void RoundTrip(Tweak tweak, BackupStore store, OsInfo os)
        {
            var before = Snapshot(tweak);
            var initial = tweak.GetState(os);
            Console.WriteLine($"    başlangıç: {initial}  [{before}]");

            tweak.Apply(store);
            Assert(tweak.GetState(os) == TweakState.Applied, $"uygulandıktan sonra durum {tweak.GetState(os)}");
            Console.WriteLine($"    uygulandı: [{Snapshot(tweak)}]");

            tweak.Apply(store); // iki kez uygulamak orijinal yedeği ezmemeli
            Assert(tweak.GetState(os) == TweakState.Applied, "ikinci uygulamadan sonra durum değişti");

            tweak.Revert(store);
            var after = Snapshot(tweak);
            Console.WriteLine($"    geri alındı: {tweak.GetState(os)}  [{after}]");
            Assert(after == before, $"geri alınca ilk hâline dönmedi\n      önce:  {before}\n      sonra: {after}");
            Assert(tweak.GetState(os) == initial, $"durum ilk hâline dönmedi ({initial} → {tweak.GetState(os)})");
            Assert(!store.HasAny(tweak.Id), "geri alındıktan sonra yedek silinmedi");
        }

        private static string Snapshot(Tweak tweak)
        {
            switch (tweak)
            {
                case RegistryTweak r:
                    return string.Join(" | ", r.Values.Select(v =>
                    {
                        var raw = Reg.Read(v.Hive, v.Path, v.Name);
                        return raw == null ? $"{v.Name}=(yok)" : $"{v.Name}={Reg.ReadKind(v.Hive, v.Path, v.Name)}:{BackupStore.EncodeValue(raw)}";
                    }));
                case DirectXSettingTweak _:
                    return DirectXSettingTweak.ReadRaw() ?? "(yok)";
                case PowerPlanTweak _:
                    var active = PowerPlans.GetActive();
                    var ours = PowerPlans.List().Count(g => PowerPlans.GetName(g) == PowerPlanTweak.PlanName);
                    return $"etkin={active} ({PowerPlans.GetName(active)}) planlar={PowerPlans.List().Count} bizim={ours}";
                case AccessibilityShortcutsTweak _:
                    return AccessibilityShortcutsTweak.Snapshot();
                default:
                    return "";
            }
        }

        private static void DirectXPreservesOtherKeys(BackupStore store)
        {
            var original = DirectXSettingTweak.ReadRaw();
            try
            {
                DirectXSettingTweak.WriteRaw("AutoHDREnable=1;VRROptimizeEnable=0;");
                var tweak = new DirectXSettingTweak { Id = "test-dx", SettingKey = "VRROptimizeEnable" };
                tweak.Apply(store);
                Equal("AutoHDREnable=1;VRROptimizeEnable=1;", DirectXSettingTweak.ReadRaw());
                tweak.Revert(store);
                Equal("AutoHDREnable=1;VRROptimizeEnable=0;", DirectXSettingTweak.ReadRaw());

                DirectXSettingTweak.WriteRaw("AutoHDREnable=1;");
                tweak.Apply(store);
                Equal("AutoHDREnable=1;VRROptimizeEnable=1;", DirectXSettingTweak.ReadRaw());
                tweak.Revert(store);
                Equal("AutoHDREnable=1;", DirectXSettingTweak.ReadRaw());
            }
            finally
            {
                DirectXSettingTweak.WriteRaw(original);
            }
        }

        private static void StartupToggle()
        {
            const string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            const string approvedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
            const string name = "AriaBoostSelfTest";
            Reg.Write(Hive.CurrentUser, runPath, name, "\"C:\\Windows\\System32\\cmd.exe\" /c exit", RegistryValueKind.String);
            try
            {
                var all = StartupManager.List();
                foreach (var i in all) Console.WriteLine($"    {(i.Enabled ? "açık " : "kapalı")} {i.Name} [{i.SourceLabel}] {i.ExecutablePath}");
                var item = all.Single(i => i.Name == name);
                Assert(item.Enabled, "yeni öğe açık görünmeli");
                Equal(@"C:\Windows\System32\cmd.exe", item.ExecutablePath);

                StartupManager.SetEnabled(item, false);
                Assert(!StartupManager.List().Single(i => i.Name == name).Enabled, "kapatılan öğe açık görünüyor");
                var data = (byte[])Reg.Read(Hive.CurrentUser, approvedPath, name);
                Assert(data.Length == 12 && data[0] == 0x03, "StartupApproved değeri Görev Yöneticisi biçiminde değil");

                StartupManager.SetEnabled(item, true);
                Assert(StartupManager.List().Single(i => i.Name == name).Enabled, "yeniden açılan öğe kapalı görünüyor");
            }
            finally
            {
                Reg.Delete(Hive.CurrentUser, runPath, name);
                Reg.Delete(Hive.CurrentUser, approvedPath, name);
            }
        }

        private static void CleanerSandbox()
        {
            var root = Path.Combine(Path.GetTempPath(), "ariaboost-clean-" + Guid.NewGuid().ToString("N"));
            var outside = root + "-outside";
            Directory.CreateDirectory(Path.Combine(root, "sub"));
            Directory.CreateDirectory(outside);
            try
            {
                var old = DateTime.Now.AddDays(-3);
                string Make(string path, bool isOld)
                {
                    File.WriteAllText(path, new string('x', 1000));
                    if (isOld) { File.SetCreationTime(path, old); File.SetLastWriteTime(path, old); }
                    return path;
                }
                var oldFile = Make(Path.Combine(root, "old.tmp"), true);
                var newFile = Make(Path.Combine(root, "new.tmp"), false);
                var oldNested = Make(Path.Combine(root, "sub", "old2.tmp"), true);
                var victim = Make(Path.Combine(outside, "victim.txt"), true);

                var link = Path.Combine(root, "link");
                var p = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{outside}\"") { UseShellExecute = false, CreateNoWindow = true });
                p.WaitForExit();
                Assert(Directory.Exists(link), "test bağlantı noktası oluşturulamadı");

                var target = new CleanTarget { Id = "sandbox", Roots = () => new[] { root }, MinAge = TimeSpan.FromHours(24) };
                var analysis = Cleaner.Analyze(target);
                Equal(2, analysis.Files);

                var result = Cleaner.Clean(target);
                Equal(2, result.Files);
                Assert(!File.Exists(oldFile) && !File.Exists(oldNested), "eski dosyalar silinmedi");
                Assert(File.Exists(newFile), "yeni dosya silindi");
                Assert(File.Exists(victim), "bağlantı noktasının hedefindeki dosya silindi!");
                Assert(!Directory.Exists(Path.Combine(root, "sub")), "boş alt klasör silinmedi");
                Assert(Directory.Exists(root), "kök klasör silinmemeli");
            }
            finally
            {
                try { Directory.Delete(Path.Combine(root, "link"), false); } catch { }
                try { Directory.Delete(root, true); } catch { }
                try { Directory.Delete(outside, true); } catch { }
            }
        }

        private static void ThemeLoads()
        {
            Exception error = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var s = typeof(Program).Assembly.GetManifestResourceStream("AriaBoost.Theme.xaml"))
                    {
                        var dict = (ResourceDictionary)XamlReader.Load(s);
                        foreach (var key in new[] { "Bg", "Accent", "NavButton", "PrimaryButton", "GhostButton", "ToggleSwitch", "Card", "Check" })
                            Assert(dict.Contains(key), "temada eksik anahtar: " + key);
                    }
                }
                catch (Exception ex) { error = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (error != null) throw error;
        }

        private static void UnitExtractExecutable()
        {
            Equal(@"C:\Program Files\A\a.exe", StartupManager.ExtractExecutable("\"C:\\Program Files\\A\\a.exe\" --min"));
            Equal(@"C:\x\b.exe", StartupManager.ExtractExecutable(@"C:\x\b.exe -background"));
            Equal(@"C:\x\c.exe", StartupManager.ExtractExecutable(@"C:\x\c.EXE"));
            Equal(null, StartupManager.ExtractExecutable("  "));
        }

        private static void UnitDirectXParse()
        {
            var map = DirectXSettingTweak.Parse("A=1;B=0;;bozuk;C=x");
            Equal("A=1;B=0;C=x;", DirectXSettingTweak.Format(map));
            map["B"] = "1";
            map["D"] = "2";
            Equal("A=1;B=1;C=x;D=2;", DirectXSettingTweak.Format(map));
            map.Remove("A");
            Equal("B=1;C=x;D=2;", DirectXSettingTweak.Format(map));
        }

        private static void UnitBackupStore()
        {
            var path = Path.Combine(Path.GetTempPath(), "ariaboost-store-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var s = new BackupStore(path);
                s.SaveIfMissing("t", "k", new BackupEntry { Exists = true, Kind = "DWord", Value = "-1" });
                s.SaveIfMissing("t", "k", new BackupEntry { Exists = true, Kind = "DWord", Value = "5" });
                var reloaded = new BackupStore(path);
                Equal("-1", reloaded.Get("t", "k").Value);
                Equal(-1, (int)BackupStore.DecodeValue("DWord", "-1"));
                Equal(unchecked((int)0xFFFFFFFF), (int)BackupStore.DecodeValue("DWord", "4294967295"));
                reloaded.Remove("t");
                Assert(!new BackupStore(path).HasAny("t"), "silinen yedek geri geldi");

                File.WriteAllText(path, "{bozuk json");
                Assert(!new BackupStore(path).HasAny("t"), "bozuk dosya yüklenememeli ama çökmemeli");
            }
            finally
            {
                foreach (var f in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*")) File.Delete(f);
            }
        }

        // ------------------------------------------------------------------

        private static void Run(string name, Action test)
        {
            Console.WriteLine($"[TEST] {name}");
            try
            {
                test();
                Console.WriteLine("    ✓ geçti");
            }
            catch (Exception ex)
            {
                failures++;
                Console.WriteLine($"    ✗ BAŞARISIZ: {ex.Message}");
                if (!(ex is AssertException)) Console.WriteLine(ex);
            }
        }

        private static void Section(string title) => Console.WriteLine($"\n===== {title} =====");

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new AssertException(message);
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new AssertException($"beklenen <{expected}>, gelen <{actual}>");
        }

        private sealed class AssertException : Exception
        {
            public AssertException(string message) : base(message) { }
        }
    }
}
