using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace LetSmall
{
    internal sealed class SettingsDialog : Window
    {
        private readonly ComboBox location;
        private readonly TextBox folder;
        private readonly TextBox suffix;
        private readonly TextBlock example;
        private readonly Grid folderRow;

        internal AppSettings Result { get; private set; }

        internal SettingsDialog(AppSettings current)
        {
            Title = "Settings";
            UiPalette.UseLightCaption(this);
            Width = 490;
            Height = 410;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");

            var root = new Grid { Margin = new Thickness(26) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var heading = UiPalette.Text("Output", 21, UiPalette.Ink);
            heading.FontWeight = FontWeights.SemiBold;
            heading.Margin = new Thickness(0, 0, 0, 22);
            root.Children.Add(heading);

            var locationGroup = Field("Save location", 1, root);
            location = new ComboBox { Height = 36, FontSize = 14 };
            location.Items.Add("Same folder as original");
            location.Items.Add("Choose folder");
            location.SelectedIndex = string.IsNullOrWhiteSpace(current.OutputFolder) ? 0 : 1;
            location.SelectionChanged += (sender, args) => UpdateFolderVisibility();
            locationGroup.Children.Add(location);

            folderRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(folderRow, 2);
            root.Children.Add(folderRow);
            folder = new TextBox { Text = current.OutputFolder ?? "", Height = 34, FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center };
            folderRow.Children.Add(folder);
            var browse = UiPalette.Button("Browse", false);
            browse.Margin = new Thickness(8, 0, 0, 0);
            browse.Click += BrowseFolder;
            Grid.SetColumn(browse, 1);
            folderRow.Children.Add(browse);
            UpdateFolderVisibility();

            var suffixGroup = Field("Filename suffix", 3, root);
            suffix = new TextBox { Text = current.Suffix ?? "_compressed", Height = 36,
                FontSize = 14, VerticalContentAlignment = VerticalAlignment.Center };
            suffix.TextChanged += (sender, args) => UpdateExample();
            suffixGroup.Children.Add(suffix);
            example = UiPalette.Text("", 13, UiPalette.Muted);
            example.TextTrimming = TextTrimming.CharacterEllipsis;
            example.Margin = new Thickness(0, 8, 0, 0);
            suffixGroup.Children.Add(example);
            UpdateExample();

            var actions = new Grid();
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(actions, 5);
            root.Children.Add(actions);
            var cancel = UiPalette.Button("Cancel", false);
            cancel.Click += (sender, args) => { DialogResult = false; };
            actions.Children.Add(cancel);
            var save = UiPalette.Button("Save settings", true);
            save.MinWidth = 150;
            save.Click += Save;
            Grid.SetColumn(save, 1);
            actions.Children.Add(save);
        }

        private static StackPanel Field(string label, int row, Grid root)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 18) };
            var title = UiPalette.Text(label, 14, UiPalette.Muted);
            title.Margin = new Thickness(0, 0, 0, 7);
            panel.Children.Add(title);
            Grid.SetRow(panel, row);
            root.Children.Add(panel);
            return panel;
        }

        private void BrowseFolder(object sender, RoutedEventArgs args)
        {
            using (var picker = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    folder.Text = picker.SelectedPath;
                }
            }
        }

        private void UpdateFolderVisibility()
        {
            if (folderRow != null)
            {
                folderRow.Visibility = location.SelectedIndex == 1
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateExample()
        {
            if (example != null)
            {
                example.Text = "Vacation.jpg  \u2192  Vacation" + suffix.Text + ".jpg";
                example.ToolTip = example.Text;
            }
        }

        private void Save(object sender, RoutedEventArgs args)
        {
            var proposedSuffix = suffix.Text.Trim();
            if (proposedSuffix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show(this, "The suffix contains invalid filename characters.",
                    "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var output = location.SelectedIndex == 0 ? "" : folder.Text.Trim();
            if (location.SelectedIndex == 1 && !Directory.Exists(output))
            {
                MessageBox.Show(this, "Choose an existing output folder.",
                    "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Result = new AppSettings { OutputFolder = output, Suffix = proposedSuffix };
            DialogResult = true;
        }
    }
}
