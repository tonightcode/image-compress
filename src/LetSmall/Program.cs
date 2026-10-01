using System;
using System.Windows;

namespace LetSmall
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var app = new Application();
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/letsmall;component/Styles.xaml", UriKind.Relative)
            });
            if (args != null && args.Length > 0 &&
                ActivationClient.IsActivationArgument(args[0]))
                app.Run(new MainWindow(new string[0], args[0]));
            else
                app.Run(new MainWindow(args));
        }
    }
}
