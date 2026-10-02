using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    /// <summary>Sayfaların paylaştığı durum: optimizasyon listesi, yedek deposu, yeniden başlatma uyarısı.</summary>
    internal sealed class AppState
    {
        public OsInfo Os { get; } = OsInfo.Current;
        public BackupStore Store { get; } = BackupStore.OpenDefault();
        public IReadOnlyList<Tweak> Tweaks { get; } = TweakCatalog.Create();

        public bool RestartRequired { get; private set; }

        /// <summary>Bir optimizasyon uygulandığında / geri alındığında tetiklenir.</summary>
        public event Action TweaksChanged;
        public event Action RestartRequiredChanged;

        public TweakState SafeState(Tweak t)
        {
            try { return t.GetState(Os); }
            catch { return TweakState.Unsupported; }
        }

        public IEnumerable<Tweak> Recommended => Tweaks.Where(t => t.Recommended && t.UnsupportedReason(Os) == null);

        public async Task SetAsync(Tweak tweak, bool apply)
        {
            await Task.Run(() =>
            {
                if (apply) tweak.Apply(Store);
                else tweak.Revert(Store);
            });
            if (tweak.RequiresRestart) MarkRestart();
            TweaksChanged?.Invoke();
        }

        /// <summary>Uygulanmamış önerilenleri uygular; hata olanları döner.</summary>
        public async Task<List<string>> ApplyRecommendedAsync()
        {
            var errors = new List<string>();
            bool restart = false;
            await Task.Run(() =>
            {
                foreach (var t in Recommended)
                {
                    try
                    {
                        if (t.GetState(Os) == TweakState.Applied) continue;
                        t.Apply(Store);
                        restart |= t.RequiresRestart;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{t.Title}: {Ui.Describe(ex)}");
                    }
                }
            });
            if (restart) MarkRestart();
            TweaksChanged?.Invoke();
            return errors;
        }

        /// <summary>Aria Boost'un yaptığı tüm değişiklikleri yedekten geri alır.</summary>
        public async Task<List<string>> RevertAllAsync()
        {
            var errors = new List<string>();
            bool restart = false;
            await Task.Run(() =>
            {
                var ids = new HashSet<string>(Store.TweakIds);
                foreach (var t in Tweaks.Where(t => ids.Contains(t.Id)))
                {
                    try
                    {
                        t.Revert(Store);
                        restart |= t.RequiresRestart;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{t.Title}: {Ui.Describe(ex)}");
                    }
                }
            });
            if (restart) MarkRestart();
            TweaksChanged?.Invoke();
            return errors;
        }

        public bool HasChanges => Store.TweakIds.Count > 0;

        private void MarkRestart()
        {
            RestartRequired = true;
            RestartRequiredChanged?.Invoke();
        }
    }
}
