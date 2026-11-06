using System;
using System.Windows;
using System.Windows.Controls;

namespace LetSmall
{
    internal sealed class UpdateDialog : Window
    {
        private readonly UpdateClient client;
        private readonly Func<bool> busy;
        private readonly Action install;
        private readonly TextBlock status;
        private readonly TextBlock notes;
        private readonly ProgressBar progress;
        private readonly Button action;
        private readonly Button cancel;

        internal UpdateDialog(UpdateClient client, Func<bool> busy, Action install)
        {
            this.client = client;
            this.busy = busy;
            this.install = install;
            Title = "Software updates";
            Width = 520;
            Height = 460;
            MinWidth = 440;
            MinHeight = 380;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.White;
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            UiPalette.UseLightCaption(this);
            var root = new DockPanel { Margin = new Thickness(26) };
            Content = root;
            var heading = new StackPanel();
            DockPanel.SetDock(heading, Dock.Top);
            root.Children.Add(heading);
            heading.Children.Add(UiPalette.Text("letsmall updates", 22, UiPalette.Ink));
            heading.Children.Add(UiPalette.Text("Current version: " + UpdateClient.CurrentVersion, 14, UiPalette.Muted));
            status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 12) };
            heading.Children.Add(status);
            var bottom = new StackPanel();
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);
            progress = new ProgressBar { Height = 8, Minimum = 0, Maximum = 100, Margin = new Thickness(0, 12, 0, 12) };
            bottom.Children.Add(progress);
            var actions = new WrapPanel();
            bottom.Children.Add(actions);
            action = UiPalette.Button("Update", true);
            action.Margin = new Thickness(0, 0, 8, 8);
            action.Click += async (sender, args) =>
            {
                if (client.Ready) install();
                else await client.DownloadAsync();
            };
            actions.Children.Add(action);
            cancel = UiPalette.Button("Cancel download", false);
            cancel.Margin = new Thickness(0, 0, 8, 8);
            cancel.Click += (sender, args) => client.CancelDownload();
            actions.Children.Add(cancel);
            var later = UiPalette.Button("Later", false);
            later.Click += (sender, args) => Close();
            actions.Children.Add(later);
            notes = new TextBlock { TextWrapping = TextWrapping.Wrap };
            root.Children.Add(new ScrollViewer { Content = notes, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            client.Changed += Refresh;
            Closed += (sender, args) => client.Changed -= Refresh;
            Loaded += async (sender, args) => await client.CheckAsync();
            Refresh();
        }

        internal void Refresh()
        {
            status.Text = client.Checking ? "Checking for updates..." : client.Error ??
                (client.Downloading ? "Downloading... " + client.Progress + "%" :
                client.Ready ? (busy() ? "Ready. Wait for compression to finish before restarting." : "Ready to restart and update.") :
                client.Available ? "Version " + client.Release.Version + " is available." :
                client.Release != null ? "You're up to date." : "Checking for updates...");
            notes.Text = client.Release == null ? "" : client.Release.Notes;
            progress.Value = client.Progress;
            progress.Visibility = client.Downloading ? Visibility.Visible : Visibility.Collapsed;
            action.Content = client.Ready ? "Restart and update" : "Update";
            action.Visibility = client.Available ? Visibility.Visible : Visibility.Collapsed;
            action.IsEnabled = !client.Downloading && !client.Checking && (!client.Ready || !busy());
            cancel.Visibility = client.Downloading ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
