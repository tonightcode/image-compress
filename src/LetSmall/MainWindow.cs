using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using ShapePath = System.Windows.Shapes.Path;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace LetSmall
{
    internal sealed class MainWindow : Window
    {
        private const string FolderIconData =
            "M 2,6 L 8,6 L 10,8 L 20,8 L 20,18 L 2,18 Z";
        private const string FileIconData =
            "M 5,3 L 14,3 L 19,8 L 19,21 L 5,21 Z M 14,3 L 14,8 L 19,8";
        private readonly List<ImageJob> jobs = new List<ImageJob>();
        private readonly Dictionary<string, ImageSource> thumbnails =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private readonly QuotaCounter quota = new QuotaCounter();
        private bool isUnlocked = LicenseStore.IsUnlocked();
        private AppSettings settings = LocalStore.Load<AppSettings>("settings.xml");
        private StackPanel rows;
        private Grid dropArea;
        private Button settingsButton;
        private Button compressButton;
        private ComboBox formatBox;
        private CancellationTokenSource currentRun;

        internal MainWindow(string[] startupPaths, string activationLink = null)
        {
            Title = "letsmall";
            UiPalette.UseLightCaption(this);
            Width = Math.Min(1040, SystemParameters.WorkArea.Width);
            Height = Math.Min(660, SystemParameters.WorkArea.Height);
            MinWidth = 700;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.White;
            FontFamily = new FontFamily("Segoe UI");
            Content = BuildLayout();
            RefreshRows();
            Activated += (sender, args) => isUnlocked = LicenseStore.IsUnlocked();
            if (startupPaths != null && startupPaths.Length > 0)
            {
                Loaded += async (sender, args) => await ImportPathsAsync(startupPaths);
            }
            if (activationLink != null)
            {
                Loaded += async (sender, args) => await ActivateAsync(activationLink);
            }
        }

        private Grid BuildLayout()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(78) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(145) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(90) });
            root.Children.Add(BuildHeader());
            dropArea = BuildDropArea();
            Grid.SetRow(dropArea, 1);
            root.Children.Add(dropArea);
            var list = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            rows = new StackPanel();
            list.Content = rows;
            Grid.SetRow(list, 2);
            root.Children.Add(list);
            var footer = BuildFooter();
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
            return root;
        }

        private Border BuildHeader()
        {
            var border = new Border { BorderBrush = UiPalette.Line,
                BorderThickness = new Thickness(0, 0, 0, 1) };
            var content = new Grid { Margin = new Thickness(26, 0, 26, 0) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            border.Child = content;
            var logo = new Grid { Width = 34, Height = 34, VerticalAlignment = VerticalAlignment.Center };
            logo.Children.Add(new Border { Background = UiPalette.Orange });
            logo.Children.Add(new Border { Background = Brushes.White,
                Margin = new Thickness(7, 7, 7, 7) });
            logo.Children.Add(new Border { Background = UiPalette.Mint,
                Margin = new Thickness(13, 13, 13, 13) });
            content.Children.Add(logo);
            var name = UiPalette.Text("letsmall", 29, UiPalette.Ink);
            name.FontWeight = FontWeights.Bold;
            name.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(name, 1);
            content.Children.Add(name);
            settingsButton = UiPalette.Button("\u2699", false);
            settingsButton.Width = 43;
            settingsButton.Height = 40;
            settingsButton.Padding = new Thickness(0);
            settingsButton.FontSize = 24;
            settingsButton.ToolTip = "Settings";
            settingsButton.Click += (sender, args) => OpenSettings();
            Grid.SetColumn(settingsButton, 2);
            content.Children.Add(settingsButton);
            return border;
        }

        private Grid BuildDropArea()
        {
            var container = new Grid { Margin = new Thickness(26, 20, 26, 17),
                Cursor = Cursors.Hand, AllowDrop = true };
            var rectangle = new Rectangle
            {
                Fill = UiPalette.Mint,
                Stroke = UiPalette.Teal,
                StrokeThickness = 1.5,
                StrokeDashArray = new DoubleCollection { 5, 4 },
                RadiusX = 7,
                RadiusY = 7
            };
            container.Children.Add(rectangle);
            var contents = new StackPanel { Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
            var plus = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(24),
                Background = UiPalette.Teal, Margin = new Thickness(0, 0, 18, 0) };
            plus.Child = new TextBlock { Text = "+", FontSize = 34, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, -6, 0, 0) };
            contents.Children.Add(plus);
            var label = UiPalette.Text("Drop files or folders here, or click to browse", 21,
                UiPalette.Ink);
            label.FontWeight = FontWeights.SemiBold;
            contents.Children.Add(label);
            container.Children.Add(contents);
            container.MouseLeftButtonUp += (sender, args) => ShowImportMenu();
            container.DragOver += OnDragOver;
            container.Drop += OnDrop;
            return container;
        }

        private Border BuildFooter()
        {
            var border = new Border { BorderBrush = UiPalette.Line,
                BorderThickness = new Thickness(0, 1, 0, 0) };
            var content = new Grid { Margin = new Thickness(26, 0, 26, 0) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            border.Child = content;
            var label = UiPalette.Text("Format", 14, UiPalette.Muted);
            label.FontWeight = FontWeights.SemiBold;
            label.Margin = new Thickness(0, 0, 12, 0);
            content.Children.Add(label);
            formatBox = new ComboBox { Width = 160, Height = 38, FontSize = 15,
                VerticalAlignment = VerticalAlignment.Center };
            foreach (var format in new[] { "Original", "JPEG", "PNG", "WebP" })
            {
                formatBox.Items.Add(format);
            }
            formatBox.SelectedIndex = 0;
            Grid.SetColumn(formatBox, 1);
            content.Children.Add(formatBox);
            compressButton = UiPalette.Button("Compress", true);
            compressButton.MinWidth = 220;
            compressButton.Height = 53;
            compressButton.FontSize = 19;
            compressButton.FontWeight = FontWeights.SemiBold;
            compressButton.Click += OnCompressClick;
            Grid.SetColumn(compressButton, 3);
            content.Children.Add(compressButton);
            return border;
        }

        private void ShowImportMenu()
        {
            if (currentRun != null)
            {
                return;
            }
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("ImportMenuStyle"),
                PlacementTarget = dropArea,
                Placement = PlacementMode.Bottom,
                HorizontalOffset = Math.Max(0, (dropArea.ActualWidth - 190) / 2),
                VerticalOffset = -8
            };
            var files = ImportMenuItem("Files", FileIconData, UiPalette.Teal);
            files.Click += async (sender, args) => await SelectFilesAsync();
            menu.Items.Add(files);
            var folder = ImportMenuItem("Folder", FolderIconData, UiPalette.Orange);
            folder.Click += async (sender, args) => await SelectFolderAsync();
            menu.Items.Add(folder);
            menu.IsOpen = true;
        }

        private MenuItem ImportMenuItem(string label, string iconData, Brush color)
        {
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(CreateIcon(iconData, color));
            var text = UiPalette.Text(label, 15, UiPalette.Ink);
            text.Margin = new Thickness(12, 0, 0, 0);
            header.Children.Add(text);
            var item = new MenuItem { Header = header,
                Style = (Style)FindResource("ImportMenuItemStyle") };
            AutomationProperties.SetName(item, label);
            return item;
        }

        private static ShapePath CreateIcon(string data, Brush color)
        {
            return new ShapePath { Data = Geometry.Parse(data), Width = 19, Height = 19,
                Stretch = Stretch.Uniform, Stroke = color, StrokeThickness = 1.8,
                StrokeLineJoin = PenLineJoin.Round };
        }

        private async Task SelectFilesAsync()
        {
            var picker = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Images|*.jpg;*.jpeg;*.png;*.webp|All files|*.*"
            };
            if (picker.ShowDialog(this) == true)
            {
                await ImportPathsAsync(picker.FileNames);
            }
        }

        private async Task SelectFolderAsync()
        {
            using (var picker = new System.Windows.Forms.FolderBrowserDialog())
            {
                if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    await ImportPathsAsync(new[] { picker.SelectedPath });
                }
            }
        }

        private void OnDragOver(object sender, DragEventArgs args)
        {
            args.Effects = currentRun == null && args.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            args.Handled = true;
        }

        private async void OnDrop(object sender, DragEventArgs args)
        {
            if (currentRun == null && args.Data.GetDataPresent(DataFormats.FileDrop))
            {
                await ImportPathsAsync((string[])args.Data.GetData(DataFormats.FileDrop));
            }
            args.Handled = true;
        }

        private async Task ImportPathsAsync(IEnumerable<string> paths)
        {
            try
            {
                var existing = jobs.Select(job => job.SourcePath).ToList();
                var found = await Task.Run(() => ImportService.FindImages(paths, existing));
                var remaining = isUnlocked ? int.MaxValue : quota.Remaining;
                var available = Math.Max(0, remaining - jobs.Count(job => job.Status == "Ready"));
                if (found.Count > available)
                {
                    var prompt = new QuotaDialog(available) { Owner = this };
                    prompt.ShowDialog();
                    if (prompt.WantsPurchase)
                    {
                        if (!ActivationClient.OpenCheckout())
                            MessageBox.Show(this, "Checkout is not configured yet.", "letsmall",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    found = found.Take(available).ToList();
                }
                foreach (var path in found)
                {
                    jobs.Add(new ImageJob(path));
                }
                RefreshRows();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, ErrorMessages.For(error), "Could not import images",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ActivateAsync(string link)
        {
            try
            {
                await ActivationClient.RedeemAsync(link);
                isUnlocked = LicenseStore.IsUnlocked();
                if (!isUnlocked)
                    throw new ActivationException("The saved license could not be verified.");
                MessageBox.Show(this, "letsmall is unlocked on this computer.",
                    "Activation complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (ActivationException error)
            {
                MessageBox.Show(this, error.Message, "Activation failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception)
            {
                MessageBox.Show(this, "Activation could not be completed.",
                    "Activation failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void OnCompressClick(object sender, RoutedEventArgs args)
        {
            if (currentRun != null)
            {
                currentRun.Cancel();
                return;
            }
            await CompressReadyAsync();
        }

        private async Task CompressReadyAsync()
        {
            var pending = jobs.Where(job => job.Status == "Ready").ToList();
            if (pending.Count == 0)
            {
                return;
            }
            currentRun = new CancellationTokenSource();
            SetRunning(true);
            var format = (string)formatBox.SelectedItem;
            var outputSettings = settings;
            try
            {
                foreach (var job in pending)
                {
                    if (currentRun.IsCancellationRequested)
                    {
                        break;
                    }
                    job.Status = "Compressing";
                    RefreshRows();
                    try
                    {
                        var path = await Task.Run(() => OutputService.Compress(job, format,
                            outputSettings, currentRun.Token));
                        job.OutputPath = path;
                        job.OutputBytes = new FileInfo(path).Length;
                        if (!isUnlocked)
                            quota.RecordSuccess();
                        job.Status = "Done";
                    }
                    catch (OperationCanceledException)
                    {
                        job.Status = "Ready";
                        break;
                    }
                    catch (Exception error)
                    {
                        job.Status = "Failed: " + ErrorMessages.For(error);
                    }
                    RefreshRows();
                }
            }
            finally
            {
                currentRun.Dispose();
                currentRun = null;
                SetRunning(false);
                RefreshRows();
            }
        }

        private void SetRunning(bool running)
        {
            dropArea.IsEnabled = !running;
            settingsButton.IsEnabled = !running;
            formatBox.IsEnabled = !running;
            if (running)
            {
                compressButton.IsEnabled = true;
                compressButton.Content = "Cancel";
            }
        }

        private void RefreshRows()
        {
            rows.Children.Clear();
            foreach (var job in jobs)
            {
                rows.Children.Add(BuildRow(job));
            }
            if (currentRun == null)
            {
                var count = jobs.Count(job => job.Status == "Ready");
                compressButton.Content = "Compress " + count + (count == 1 ? " image" : " images");
                compressButton.IsEnabled = count > 0;
                compressButton.Foreground = count > 0 ? Brushes.White : UiPalette.Muted;
                compressButton.Background = count > 0 ? UiPalette.Orange
                    : UiPalette.Brush(240, 243, 247);
            }
        }

        private Border BuildRow(ImageJob job)
        {
            var border = new Border { BorderBrush = UiPalette.Line,
                BorderThickness = new Thickness(0, 0, 0, 1), Height = 96 };
            var content = new Grid { Margin = new Thickness(26, 10, 26, 10) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            border.Child = content;
            content.Children.Add(BuildThumbnail(job.SourcePath));
            var description = new StackPanel { VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 12, 0) };
            var name = UiPalette.Text(Path.GetFileName(job.SourcePath), 17, UiPalette.Ink);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            description.Children.Add(name);
            var status = UiPalette.Text(StatusText(job), 13, UiPalette.Muted);
            status.TextTrimming = TextTrimming.CharacterEllipsis;
            status.ToolTip = job.Status.StartsWith("Failed:") ? job.Status : null;
            description.Children.Add(status);
            Grid.SetColumn(description, 1);
            content.Children.Add(description);
            var actions = new StackPanel { Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center };
            var actionStyle = (Style)FindResource("RowIconButtonStyle");
            var reveal = new Button { Content = CreateIcon(FolderIconData, UiPalette.Teal),
                Style = actionStyle, Width = 36, Height = 36,
                ToolTip = "Show source in folder", Margin = new Thickness(0, 0, 4, 0) };
            AutomationProperties.SetName(reveal, "Show source in folder");
            reveal.Click += (sender, args) => ShowSourceInFolder(job.SourcePath);
            actions.Children.Add(reveal);
            var remove = new Button { Content = "\u00d7", Style = actionStyle };
            remove.Width = 35;
            remove.Height = 35;
            remove.FontSize = 24;
            remove.ToolTip = "Remove image";
            AutomationProperties.SetName(remove, "Remove image");
            remove.IsEnabled = currentRun == null;
            remove.Click += (sender, args) =>
            {
                jobs.Remove(job);
                thumbnails.Remove(job.SourcePath);
                RefreshRows();
            };
            actions.Children.Add(remove);
            Grid.SetColumn(actions, 2);
            content.Children.Add(actions);
            return border;
        }

        private void ShowSourceInFolder(string path)
        {
            if (!File.Exists(path))
            {
                MessageBox.Show(this, "The source image no longer exists.",
                    "Could not locate image", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"")
                {
                    UseShellExecute = true
                });
            }
            catch (Exception error) when (error is Win32Exception ||
                error is InvalidOperationException)
            {
                MessageBox.Show(this, "Could not open File Explorer.",
                    "Could not locate image", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private Border BuildThumbnail(string path)
        {
            var frame = new Border { Width = 64, Height = 64, CornerRadius = new CornerRadius(5),
                Background = UiPalette.Mint, ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center };
            try
            {
                ImageSource source;
                if (!thumbnails.TryGetValue(path, out source))
                {
                    source = LoadThumbnail(path);
                    thumbnails[path] = source;
                }
                frame.Child = new Image { Source = source, Stretch = Stretch.UniformToFill };
                return frame;
            }
            catch (Exception) { }
            var label = UiPalette.Text(ImageCodec.FormatFromPath(path) ?? "IMG", 11,
                UiPalette.Teal);
            label.FontWeight = FontWeights.Bold;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            frame.Child = label;
            return frame;
        }

        private static ImageSource LoadThumbnail(string path)
        {
            if (ImageCodec.FormatFromPath(path) != "WebP")
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = 70;
                image.UriSource = new Uri(path);
                image.EndInit();
                image.Freeze();
                return image;
            }

            var source = ImageCodec.Decode(path);
            var scale = Math.Max(64d / source.PixelWidth, 64d / source.PixelHeight);
            var width = source.PixelWidth * scale;
            var height = source.PixelHeight * scale;
            var drawing = new DrawingVisual();
            using (var context = drawing.RenderOpen())
            {
                context.DrawImage(source, new Rect((64 - width) / 2, (64 - height) / 2,
                    width, height));
            }
            var thumbnail = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
            thumbnail.Render(drawing);
            thumbnail.Freeze();
            return thumbnail;
        }

        private static string StatusText(ImageJob job)
        {
            if (job.Status == "Ready")
            {
                return (job.SourceBytes / 1048576d).ToString("0.0") + " MB";
            }
            if (job.Status == "Done")
            {
                return "Done  \u00b7  " + (job.OutputBytes / 1048576d).ToString("0.0") + " MB";
            }
            return job.Status;
        }

        private void OpenSettings()
        {
            var dialog = new SettingsDialog(settings) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                settings = dialog.Result;
                LocalStore.Save("settings.xml", settings);
            }
        }
    }
}
