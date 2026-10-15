using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace LetSmall
{
    internal static class UiPalette
    {
        internal static readonly Brush Ink = Brush(16, 19, 35);
        internal static readonly Brush Muted = Brush(88, 100, 137);
        internal static readonly Brush Line = Brush(217, 225, 235);
        internal static readonly Brush Orange = Brush(255, 76, 62);
        internal static readonly Brush Mint = Brush(232, 250, 246);
        internal static readonly Brush Teal = Brush(0, 175, 165);

        internal static Brush Brush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        internal static Button Button(string label, bool primary)
        {
            return new Button
            {
                Content = label,
                Background = primary ? Orange : Brushes.White,
                Foreground = primary ? Brushes.White : Ink,
                BorderBrush = primary ? Orange : Line,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 9, 18, 9),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 15,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        internal static TextBlock Text(string value, double size, Brush color)
        {
            return new TextBlock
            {
                Text = value,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = size,
                Foreground = color,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        internal static void UseLightCaption(Window window)
        {
            window.UseLayoutRounding = true;
            window.SnapsToDevicePixels = true;
            window.SourceInitialized += (sender, args) =>
            {
                var handle = new WindowInteropHelper(window).Handle;
                var darkText = 0x00231310;
                var white = 0x00FFFFFF;
                DwmSetWindowAttribute(handle, 36, ref darkText, sizeof(int));
                DwmSetWindowAttribute(handle, 35, ref white, sizeof(int));
            };
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute,
            ref int value, int size);
    }
}
