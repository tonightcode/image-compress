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
        private readonly Border statusCard;
        private string previousStage;
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
            status = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center, Foreground = UiPalette.Ink };
            statusCard = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(14),
                MinHeight = 70, Margin = new Thickness(0, 18, 0, 16), Child = status };
            heading.Children.Add(statusCard);
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
            later.Margin = new Thickness(0, 0, 0, 8);
            later.Click += (sender, args) => Close();
            actions.Children.Add(later);
            notes = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14,
                Foreground = UiPalette.Muted, LineHeight = 22 };
            root.Children.Add(new ScrollViewer { Content = notes, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            client.Changed += Refresh;
            Closed += (sender, args) => client.Changed -= Refresh;
            Loaded += async (sender, args) => await client.CheckAsync();
            Refresh();
        }

        internal void Refresh()
        {
            var stage = client.Checking ? "checking" : client.Error != null ? "error" :
                client.Downloading ? "downloading" : client.Ready ? "ready" :
                client.Available ? "available" : "current";
            statusCard.Background = stage == "error" ? UiPalette.Brush(255, 241, 240) :
                stage == "ready" || stage == "current" ? UiPalette.Mint : UiPalette.Brush(245, 247, 251);
            status.Foreground = stage == "error" ? UiPalette.Brush(180, 35, 24) : UiPalette.Ink;
            status.Text = client.Checking ? "Checking for updates..." : client.Error ??
                (client.Downloading ? "Downloading... " + client.Progress + "%" :
                client.Ready ? (busy() ? "Ready. Wait for compression to finish before restarting." : "Ready to restart and update.") :
                client.Available ? "Version " + client.Release.Version + " is available." :
                client.Release != null ? "You're up to date." : "Checking for updates...");
            notes.Text = client.Release == null ? "Updates are optional. You can keep using letsmall while checking or downloading." :
                "What's new\n\n" + client.Release.Notes;
            progress.Value = client.Progress;
            progress.IsIndeterminate = client.Checking && SystemParameters.ClientAreaAnimation;
            progress.Visibility = client.Downloading || client.Checking ? Visibility.Visible : Visibility.Collapsed;
            action.Content = client.Ready ? "Restart and update" : client.Error != null ? "Try download again" : "Download update";
            action.Visibility = client.Available ? Visibility.Visible : Visibility.Collapsed;
            action.IsEnabled = !client.Downloading && !client.Checking && (!client.Ready || !busy());
            cancel.Visibility = client.Downloading ? Visibility.Visible : Visibility.Collapsed;
            // Progress updates do not replay entrance effects for each downloaded chunk.
            if (stage != previousStage && IsLoaded) UiMotion.Reveal(statusCard);
            previousStage = stage;
        }
    }
}
