using System;
using System.Windows;
using System.Threading;

namespace LetSmall
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            using (var running = new Mutex(false, @"Local\letsmall-running"))
            {
                try
                {
                    using (Mutex.OpenExisting(@"Local\letsmall-installing"))
                    {
                        MessageBox.Show("letsmall is being updated. Please open it again when installation finishes.");
                        return;
                    }
                }
                catch (WaitHandleCannotBeOpenedException) { }
                Run(args);
                GC.KeepAlive(running);
            }
        }

        private static void Run(string[] args)
        {
            var app = new Application();
            UiMotion.Initialize(app);
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
