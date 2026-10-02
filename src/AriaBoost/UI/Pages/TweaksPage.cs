using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AriaBoost.Core;

namespace AriaBoost.UI
{
    /// <summary>Tüm optimizasyonlar, kategorilere göre; her biri anahtarla açılıp kapanır.</summary>
    internal sealed class TweaksPage : ContentControl, IPage
    {
        private readonly AppState state;
        private readonly Window owner;
        private readonly List<Row> rows = new List<Row>();

        private sealed class Row
        {
            public Tweak Tweak;
            public CheckBox Toggle;
            public TextBlock StateNote;
            public bool Busy;
        }

        public TweaksPage(AppState state, Window owner)
        {
            this.state = state;
            this.owner = owner;

            var body = new StackPanel();
            body.Children.Add(Ui.Header("Optimizasyonlar",
                "Her ayar açıldığında önce orijinal değeri yedeklenir; kapatınca birebir eski hâline döner. " +
                "Güvenliği azaltan (Defender, güncellemeler, Spectre korumaları) veya etkisi kanıtlanmamış ayarlar bilerek eklenmedi."));

            var categories = new[] { Categories.Gaming, Categories.System, Categories.Input, Categories.Network };
            foreach (var category in categories)
            {
                var tweaks = state.Tweaks.Where(t => t.Category == category).ToList();
                if (tweaks.Count == 0) continue;

                var title = Ui.Text(category, 15, "Text", FontWeights.SemiBold);
                title.Margin = new Thickness(2, body.Children.Count > 1 ? 26 : 0, 0, 12);
                body.Children.Add(title);

                var list = new StackPanel();
                foreach (var t in tweaks)
                {
                    if (list.Children.Count > 0) list.Children.Add(new Border { Height = 1, Background = Ui.Brush("Border") });
                    list.Children.Add(BuildRow(t));
                }
                var card = Ui.Card(list);
                card.Padding = new Thickness(22, 4, 22, 4);
                body.Children.Add(card);
            }

            Content = Ui.Page(body);
            state.TweaksChanged += Refresh;
        }

        public void OnShown() => Refresh();

        private FrameworkElement BuildRow(Tweak tweak)
        {
            var row = new Row { Tweak = tweak };
            var grid = new Grid { Margin = new Thickness(0, 16, 0, 16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var text = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
            var titleLine = new WrapPanel { Orientation = Orientation.Horizontal };
            var title = Ui.Text(tweak.Title, 14.5, "Text", FontWeights.SemiBold);
            title.Margin = new Thickness(0, 0, 0, 0);
            titleLine.Children.Add(title);

            var unsupported = tweak.UnsupportedReason(state.Os);
            if (unsupported != null)
            {
                titleLine.Children.Add(Ui.Badge(unsupported, "TextDim", "Border"));
            }
            else
            {
                if (tweak.Recommended) titleLine.Children.Add(Ui.Badge("Önerilen", "Accent2", "AccentSoft"));
                titleLine.Children.Add(Ui.Badge(ImpactLabel(tweak.Impact), ImpactBrush(tweak.Impact), "Border"));
                if (tweak.RequiresRestart) titleLine.Children.Add(Ui.Badge("Yeniden başlatma gerekir", "Warning", "WarningSoft"));
            }
            text.Children.Add(titleLine);

            var desc = Ui.Text(tweak.Description, 13, "TextDim", wrap: true);
            desc.Margin = new Thickness(0, 6, 0, 0);
            desc.LineHeight = 19;
            text.Children.Add(desc);

            row.StateNote = Ui.Text("", 12, "Warning");
            row.StateNote.Margin = new Thickness(0, 6, 0, 0);
            row.StateNote.Visibility = Visibility.Collapsed;
            text.Children.Add(row.StateNote);
            grid.Children.Add(text);

            row.Toggle = new CheckBox { Style = Ui.Style("ToggleSwitch"), VerticalAlignment = VerticalAlignment.Center, IsEnabled = unsupported == null };
            row.Toggle.Click += async (_, __) =>
            {
                if (row.Busy) return;
                bool apply = row.Toggle.IsChecked == true;
                row.Busy = true;
                row.Toggle.IsEnabled = false;
                try
                {
                    await state.SetAsync(tweak, apply);
                }
                catch (Exception ex)
                {
                    Ui.Error(owner, $"\"{tweak.Title}\" {(apply ? "uygulanamadı" : "geri alınamadı")}:\n\n{Ui.Describe(ex)}");
                }
                finally
                {
                    row.Busy = false;
                    Refresh();
                }
            };
            Grid.SetColumn(row.Toggle, 1);
            grid.Children.Add(row.Toggle);

            if (unsupported != null) grid.Opacity = 0.55;
            rows.Add(row);
            return grid;
        }

        private void Refresh()
        {
            foreach (var row in rows)
            {
                if (row.Busy) continue;
                var s = state.SafeState(row.Tweak);
                row.Toggle.IsChecked = s == TweakState.Applied;
                row.Toggle.IsEnabled = s != TweakState.Unsupported;
                row.StateNote.Visibility = s == TweakState.Partial ? Visibility.Visible : Visibility.Collapsed;
                row.StateNote.Text = "Kısmen uygulanmış — tamamlamak için anahtarı aç.";
            }
        }

        private static string ImpactLabel(Impact i) =>
            i == Impact.High ? "Etki: yüksek" : i == Impact.Medium ? "Etki: orta" : "Etki: düşük";

        private static string ImpactBrush(Impact i) =>
            i == Impact.High ? "Success" : i == Impact.Medium ? "Text" : "TextDim";
    }
}
