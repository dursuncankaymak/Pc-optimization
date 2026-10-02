using System;

namespace AriaBoost.Core
{
    public enum TweakState { Applied, NotApplied, Partial, Unsupported }

    public enum Impact { Low, Medium, High }

    public static class Categories
    {
        public const string Gaming = "Oyun";
        public const string System = "Sistem";
        public const string Input = "Fare ve klavye";
        public const string Network = "Ağ";
    }

    /// <summary>
    /// Tek bir optimizasyon. Her optimizasyon uygulanabilir, durumu okunabilir ve
    /// orijinal değerlere birebir geri alınabilir olmak zorundadır.
    /// </summary>
    public abstract class Tweak
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public Impact Impact { get; set; } = Impact.Medium;
        public bool RequiresRestart { get; set; }
        public bool Recommended { get; set; }
        public int MinBuild { get; set; } = OsInfo.MinSupportedBuild;

        /// <summary>Desteklenmiyorsa nedenini döner (ör. "Windows 11 gerektirir").</summary>
        public virtual string UnsupportedReason(OsInfo os)
        {
            if (os.Build >= MinBuild) return null;
            return MinBuild >= OsInfo.Windows11Build
                ? "Windows 11 gerektirir"
                : $"Windows derleme {MinBuild} veya üstünü gerektirir";
        }

        public TweakState GetState(OsInfo os) => UnsupportedReason(os) != null ? TweakState.Unsupported : ReadState();

        protected abstract TweakState ReadState();

        /// <summary>Değiştirmeden önce orijinali yedekler, sonra uygular.</summary>
        public abstract void Apply(BackupStore store);

        /// <summary>Yedekteki orijinal değerlere döner; yedek yoksa Windows varsayılanına.</summary>
        public abstract void Revert(BackupStore store);

        public override string ToString() => Id;
    }

    public sealed class TweakException : Exception
    {
        public TweakException(string message, Exception inner = null) : base(message, inner) { }
    }
}
