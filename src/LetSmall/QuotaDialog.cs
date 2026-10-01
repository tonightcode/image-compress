using System.Windows;
using System.Windows.Controls;

namespace LetSmall
{
    internal sealed class QuotaDialog : Window
    {
        internal bool WantsPurchase { get; private set; }

        internal QuotaDialog(int remaining)
        {
            Title = "Unlock letsmall";
            UiPalette.UseLightCaption(this);
            Width = 500;
            Height = 315;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

            var root = new Grid { Margin = new Thickness(26, 22, 26, 22) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var heading = UiPalette.Text("More than 3 images?", 25, UiPalette.Ink);
            heading.FontWeight = FontWeights.SemiBold;
            heading.HorizontalAlignment = HorizontalAlignment.Center;
            root.Children.Add(heading);

            var message = UiPalette.Text(
                "You can compress 3 images per day for free.\nUnlock unlimited images with a one-time purchase.",
                14, UiPalette.Muted);
            message.TextAlignment = TextAlignment.Center;
            message.HorizontalAlignment = HorizontalAlignment.Center;
            message.Margin = new Thickness(0, 16, 0, 0);
            Grid.SetRow(message, 1);
            root.Children.Add(message);

            var price = UiPalette.Text("$6.90  \u00b7  pay once", 22, UiPalette.Orange);
            price.FontWeight = FontWeights.SemiBold;
            price.HorizontalAlignment = HorizontalAlignment.Center;
            price.Margin = new Thickness(0, 21, 0, 0);
            Grid.SetRow(price, 2);
            root.Children.Add(price);

            var terms = UiPalette.Text(
                "One computer only. No transfers. If the local license is lost, a new purchase is required.",
                12, UiPalette.Muted);
            terms.TextWrapping = TextWrapping.Wrap;
            terms.TextAlignment = TextAlignment.Center;
            terms.HorizontalAlignment = HorizontalAlignment.Center;
            terms.VerticalAlignment = VerticalAlignment.Center;
            terms.MaxWidth = 410;
            Grid.SetRow(terms, 3);
            root.Children.Add(terms);

            var actions = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center };
            Grid.SetRow(actions, 4);
            root.Children.Add(actions);
            var keep = UiPalette.Button(remaining > 0 ? "Keep first " + remaining : "Not now", false);
            keep.MinWidth = 145;
            keep.Click += (sender, args) => { DialogResult = false; };
            actions.Children.Add(keep);
            var unlock = UiPalette.Button("Unlock for $6.90", true);
            unlock.MinWidth = 170;
            unlock.Margin = new Thickness(12, 0, 0, 0);
            unlock.Click += (sender, args) => { WantsPurchase = true; DialogResult = true; };
            actions.Children.Add(unlock);
        }
    }
}
