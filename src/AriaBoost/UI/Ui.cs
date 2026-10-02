using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AriaBoost.UI
{
    /// <summary>Kod ile arayüz kurmak için küçük yardımcılar. Görsel stiller UI/Theme.xaml'dadır.</summary>
    internal static class Ui
    {
        public static Brush Brush(string key) => (Brush)Application.Current.Resources[key];

        public static Style Style(string key) => (Style)Application.Current.Resources[key];

        public static TextBlock Text(string text, double size = 13, string brush = "Text", FontWeight? weight = null, bool wrap = false)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = size,
                Foreground = Brush(brush),
                FontWeight = weight ?? FontWeights.Normal,
                TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            };
        }

        public static TextBlock Icon(string glyph, double size = 16, string brush = "Text")
        {
            return new TextBlock
            {
                Text = glyph,
                FontFamily = (FontFamily)Application.Current.Resources["IconFont"],
                FontSize = size,
                Foreground = Brush(brush),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        public static Border Card(UIElement child, Thickness? margin = null)
        {
            return new Border { Style = Style("Card"), Child = child, Margin = margin ?? new Thickness(0) };
        }

        public static Button Primary(string text, RoutedEventHandler onClick)
        {
            var b = new Button { Style = Style("PrimaryButton"), Content = text };
            b.Click += onClick;
            return b;
        }

        public static Button Ghost(string text, RoutedEventHandler onClick, string glyph = null)
        {
            object content = text;
            if (glyph != null)
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(Icon(glyph, 13));
                var t = Text(text);
                t.Margin = new Thickness(8, 0, 0, 0);
                sp.Children.Add(t);
                content = sp;
            }
            var b = new Button { Style = Style("GhostButton"), Content = content };
            b.Click += onClick;
            return b;
        }

        public static Border Badge(string text, string fg, string bg)
        {
            return new Border
            {
                Background = Brush(bg),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 2, 7, 3),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brush(fg) },
            };
        }

        public static System.Windows.Shapes.Ellipse Dot(string brush, double size = 8)
        {
            return new System.Windows.Shapes.Ellipse
            {
                Width = size,
                Height = size,
                Fill = Brush(brush),
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        /// <summary>Sayfa başlığı + açıklama.</summary>
        public static StackPanel Header(string title, string subtitle)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 22) };
            sp.Children.Add(Text(title, 26, "Text", FontWeights.Bold));
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = Text(subtitle, 13.5, "TextDim", wrap: true);
                sub.Margin = new Thickness(0, 6, 0, 0);
                sub.MaxWidth = 760;
                sub.HorizontalAlignment = HorizontalAlignment.Left;
                sp.Children.Add(sub);
            }
            return sp;
        }

        /// <summary>Kaydırılabilir sayfa gövdesi.</summary>
        public static ScrollViewer Page(UIElement content)
        {
            return new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border { Padding = new Thickness(36, 30, 36, 36), Child = content },
            };
        }

        public static void Error(Window owner, string message) =>
            MessageBox.Show(owner, message, "Aria Boost", MessageBoxButton.OK, MessageBoxImage.Warning);

        public static bool Confirm(Window owner, string message) =>
            MessageBox.Show(owner, message, "Aria Boost", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        public static string Describe(Exception ex) =>
            ex is Core.TweakException || ex is UnauthorizedAccessException ? ex.Message : ex.Message + $" ({ex.GetType().Name})";
    }
}
