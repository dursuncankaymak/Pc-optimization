using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    /// <summary>Gereksiz dosya temizliği.</summary>
    internal sealed class CleanerPage : ContentControl, IPage
    {
        private readonly Window owner;
        private readonly List<Row> rows = new List<Row>();
        private readonly Button scanButton;
        private readonly Button cleanButton;
        private readonly TextBlock result = Ui.Text("", 13.5, "Success", wrap: true);
        private bool scanned;
        private bool busy;

        private sealed class Row
        {
            public CleanTarget Target;
            public CheckBox Check;
            public TextBlock Size;
            public CleanResult Analysis;
        }

        public CleanerPage(Window owner)
        {
            this.owner = owner;
            var body = new StackPanel();
            body.Children.Add(Ui.Header("Temizlik",
                "Diskte yer kaplayan gereksiz dosyalar. Kullanımdaki dosyalar ve son 24 saatte oluşturulan geçici dosyalar atlanır. " +
                "Ekran kartı gölgelendirici önbelleklerine bilerek dokunulmaz: silinirlerse oyunlar ilk açılışta takılır."));

            var list = new StackPanel();
            foreach (var target in Cleaner.CreateTargets())
            {
                if (list.Children.Count > 0) list.Children.Add(new Border { Height = 1, Background = Ui.Brush("Border") });
                list.Children.Add(BuildRow(target));
            }
            var card = Ui.Card(list);
            card.Padding = new Thickness(22, 4, 22, 4);
            body.Children.Add(card);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
            cleanButton = Ui.Primary("Seçilenleri temizle", async (_, __) => await CleanAsync());
            scanButton = Ui.Ghost("Yeniden tara", async (_, __) => await ScanAsync(), Glyphs.Refresh);
            scanButton.Margin = new Thickness(12, 0, 0, 0);
            actions.Children.Add(cleanButton);
            actions.Children.Add(scanButton);
            body.Children.Add(actions);

            result.Margin = new Thickness(2, 16, 0, 0);
            body.Children.Add(result);

            Content = Ui.Page(body);
        }

        public void OnShown()
        {
            if (!scanned) _ = ScanAsync();
        }

        private FrameworkElement BuildRow(CleanTarget target)
        {
            var row = new Row { Target = target };
            var grid = new Grid { Margin = new Thickness(0, 14, 0, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Check = new CheckBox { Style = Ui.Style("Check"), IsChecked = target.DefaultSelected, VerticalAlignment = VerticalAlignment.Center };
            row.Check.Click += (_, __) => UpdateButtons();
            grid.Children.Add(row.Check);

            var text = new StackPanel { Margin = new Thickness(14, 0, 20, 0) };
            text.Children.Add(Ui.Text(target.Title, 14, "Text", FontWeights.SemiBold));
            var desc = Ui.Text(target.Description, 12.5, "TextDim", wrap: true);
            desc.Margin = new Thickness(0, 4, 0, 0);
            text.Children.Add(desc);
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);

            row.Size = Ui.Text("…", 14, "Text", FontWeights.SemiBold);
            row.Size.VerticalAlignment = VerticalAlignment.Center;
            row.Size.MinWidth = 90;
            row.Size.TextAlignment = TextAlignment.Right;
            Grid.SetColumn(row.Size, 2);
            grid.Children.Add(row.Size);

            rows.Add(row);
            return grid;
        }

        private async Task ScanAsync()
        {
            if (busy) return;
            busy = true;
            UpdateButtons();
            foreach (var r in rows) r.Size.Text = "Taranıyor…";
            try
            {
                foreach (var r in rows)
                {
                    r.Analysis = await Task.Run(() => Cleaner.Analyze(r.Target));
                    r.Size.Text = r.Analysis.Files == 0 ? "Boş" : SystemInfo.FormatBytes(r.Analysis.Bytes);
                }
                scanned = true;
            }
            finally
            {
                busy = false;
                UpdateButtons();
            }
        }

        private async Task CleanAsync()
        {
            var selected = rows.Where(r => r.Check.IsChecked == true).ToList();
            if (selected.Count == 0 || busy) return;
            if (selected.Any(r => r.Target.IsRecycleBin) &&
                !Ui.Confirm(owner, "Geri Dönüşüm Kutusu kalıcı olarak boşaltılacak. Devam edilsin mi?"))
            {
                return;
            }

            busy = true;
            UpdateButtons();
            long bytes = 0;
            int files = 0, skipped = 0;
            try
            {
                foreach (var r in selected)
                {
                    r.Size.Text = "Temizleniyor…";
                    var res = await Task.Run(() => Cleaner.Clean(r.Target));
                    bytes += res.Bytes;
                    files += res.Files;
                    skipped += res.Skipped;
                }
            }
            catch (Exception ex)
            {
                Ui.Error(owner, "Temizlik sırasında hata: " + ex.Message);
            }
            finally
            {
                busy = false;
            }

            result.Text = $"{SystemInfo.FormatBytes(bytes)} boşaltıldı ({files} dosya)." +
                          (skipped > 0 ? $" Kullanımda olan {skipped} dosya atlandı." : "");
            await ScanAsync();
        }

        private void UpdateButtons()
        {
            scanButton.IsEnabled = !busy;
            cleanButton.IsEnabled = !busy && rows.Any(r => r.Check.IsChecked == true);
        }
    }
}
