using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AriaBoost.Core
{
    /// <summary>Temizlenebilecek bir alan (ör. geçici dosyalar).</summary>
    public sealed class CleanTarget
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool DefaultSelected { get; set; } = true;

        /// <summary>Taranacak klasörler. Kök klasörlerin kendisi asla silinmez.</summary>
        public Func<IEnumerable<string>> Roots { get; set; }

        /// <summary>Bu süreden yeni dosyalara dokunulmaz (o an çalışan kurulumlar vb. bozulmasın).</summary>
        public TimeSpan MinAge { get; set; } = TimeSpan.Zero;

        /// <summary>Klasör yerine özel işlem (Geri Dönüşüm Kutusu).</summary>
        public bool IsRecycleBin { get; set; }
    }

    public sealed class CleanResult
    {
        public long Bytes { get; set; }
        public int Files { get; set; }
        public int Skipped { get; set; }
    }

    /// <summary>
    /// Disk temizliği. Güvenlik kuralları:
    ///  - Bağlantı noktalarına / sembolik bağlara (reparse point) asla girilmez; böylece
    ///    başka bir sürücüye yönlenen bir klasörün içi yanlışlıkla silinemez.
    ///  - Kullanımdaki dosyalar atlanır.
    ///  - Ekran kartı gölgelendirici (shader) önbelleklerine dokunulmaz: silinirse oyunlar
    ///    onları yeniden derlerken takılır.
    /// </summary>
    public static class Cleaner
    {
        public static IReadOnlyList<CleanTarget> CreateTargets()
        {
            string windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            return new List<CleanTarget>
            {
                new CleanTarget
                {
                    Id = "user-temp",
                    Title = "Geçici dosyalar",
                    Description = "Programların geride bıraktığı geçici dosyalar. Son 24 saatte oluşturulanlara dokunulmaz.",
                    Roots = () => new[] { Path.GetTempPath() },
                    MinAge = TimeSpan.FromHours(24),
                },
                new CleanTarget
                {
                    Id = "windows-temp",
                    Title = "Windows geçici dosyaları",
                    Description = "Windows'un ve kurulumların geçici dosyaları. Son 24 saattekilere dokunulmaz.",
                    Roots = () => new[] { Path.Combine(windir, "Temp") },
                    MinAge = TimeSpan.FromHours(24),
                },
                new CleanTarget
                {
                    Id = "crash-reports",
                    Title = "Hata raporları ve çökme dökümleri",
                    Description = "Çöken programlar için Windows'un sakladığı raporlar. Sorun gidermek için gerekmiyorsa güvenle silinebilir.",
                    Roots = () => new[]
                    {
                        Path.Combine(programData, @"Microsoft\Windows\WER\ReportArchive"),
                        Path.Combine(programData, @"Microsoft\Windows\WER\ReportQueue"),
                        Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportArchive"),
                        Path.Combine(localAppData, @"Microsoft\Windows\WER\ReportQueue"),
                        Path.Combine(localAppData, "CrashDumps"),
                    },
                },
                new CleanTarget
                {
                    Id = "recycle-bin",
                    Title = "Geri Dönüşüm Kutusu",
                    Description = "Geri Dönüşüm Kutusu'ndaki dosyalar kalıcı olarak silinir.",
                    IsRecycleBin = true,
                    DefaultSelected = false,
                },
            };
        }

        public static CleanResult Analyze(CleanTarget target)
        {
            if (target.IsRecycleBin) return QueryRecycleBin();
            var result = new CleanResult();
            var cutoff = DateTime.UtcNow - target.MinAge;
            foreach (var root in ExistingRoots(target))
            {
                foreach (var file in EnumerateFiles(new DirectoryInfo(root)))
                {
                    if (!IsOldEnough(file, cutoff)) continue;
                    result.Bytes += SafeLength(file);
                    result.Files++;
                }
            }
            return result;
        }

        public static CleanResult Clean(CleanTarget target)
        {
            if (target.IsRecycleBin)
            {
                var before = QueryRecycleBin();
                Native.SHEmptyRecycleBin(IntPtr.Zero, null, Native.SHERB_NOCONFIRMATION | Native.SHERB_NOPROGRESSUI | Native.SHERB_NOSOUND);
                return before;
            }

            var result = new CleanResult();
            var cutoff = DateTime.UtcNow - target.MinAge;
            foreach (var root in ExistingRoots(target))
            {
                var rootDir = new DirectoryInfo(root);
                foreach (var file in EnumerateFiles(rootDir).ToList())
                {
                    if (!IsOldEnough(file, cutoff)) continue;
                    long length = SafeLength(file);
                    try
                    {
                        if ((file.Attributes & FileAttributes.ReadOnly) != 0) file.Attributes &= ~FileAttributes.ReadOnly;
                        file.Delete();
                        result.Bytes += length;
                        result.Files++;
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        result.Skipped++; // kullanımda ya da izin yok
                    }
                }
                RemoveEmptyDirectories(rootDir, isRoot: true);
            }
            return result;
        }

        private static IEnumerable<string> ExistingRoots(CleanTarget target) =>
            (target.Roots?.Invoke() ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r))
                .Select(r => Path.GetFullPath(r))
                .Distinct(StringComparer.OrdinalIgnoreCase);

        /// <summary>Reparse point'lere girmeden dosyaları gezer.</summary>
        public static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo dir)
        {
            var stack = new Stack<DirectoryInfo>();
            stack.Push(dir);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                FileSystemInfo[] entries;
                try { entries = current.GetFileSystemInfos(); }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException) { continue; }

                foreach (var entry in entries)
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    if (entry is DirectoryInfo sub) stack.Push(sub);
                    else if (entry is FileInfo file) yield return file;
                }
            }
        }

        private static void RemoveEmptyDirectories(DirectoryInfo dir, bool isRoot)
        {
            DirectoryInfo[] subs;
            try { subs = dir.GetDirectories(); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { return; }

            foreach (var sub in subs)
            {
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                RemoveEmptyDirectories(sub, isRoot: false);
            }
            if (isRoot) return;
            try
            {
                if (!dir.EnumerateFileSystemInfos().Any()) dir.Delete(false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException) { }
        }

        private static bool IsOldEnough(FileInfo file, DateTime cutoffUtc)
        {
            try { return file.LastWriteTimeUtc <= cutoffUtc && file.CreationTimeUtc <= cutoffUtc; }
            catch (IOException) { return false; }
        }

        private static long SafeLength(FileInfo file)
        {
            try { return file.Length; }
            catch (IOException) { return 0; }
        }

        private static CleanResult QueryRecycleBin()
        {
            var info = new Native.SHQUERYRBINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.SHQUERYRBINFO)) };
            if (Native.SHQueryRecycleBin(null, ref info) != 0) return new CleanResult();
            return new CleanResult { Bytes = info.i64Size, Files = (int)Math.Min(int.MaxValue, info.i64NumItems) };
        }
    }
}
