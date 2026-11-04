using System;
using System.IO;
using System.Windows;

namespace WebAgentManagerApp
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    File.AppendAllText(Path.Combine(PathHelper.GetDataRoot(), "crash.log"),
                        DateTime.Now.ToString("s") + " [AppDomain] " + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "Unknown") + "\r\n");
                }
                catch { }
            };

            if (args != null && args.Length > 0 && args[0].StartsWith("web-agent:", StringComparison.OrdinalIgnoreCase))
            {
                SmartRouter.RouteWebAgentCall(args[0]);
                return;
            }

            try
            {
                Application app = new Application();
                app.DispatcherUnhandledException += (s, e) =>
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(PathHelper.GetDataRoot(), "crash.log"),
                            DateTime.Now.ToString("s") + " [Dispatcher] " + e.Exception.ToString() + "\r\n");
                    }
                    catch { }
                    e.Handled = true;
                };
                app.Run(new MainWindow());
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(PathHelper.GetDataRoot(), "crash.log"),
                        DateTime.Now.ToString("s") + " [MainCatch] " + ex.ToString() + "\r\n");
                }
                catch { }
                MessageBox.Show("Error al iniciar WebAgent Manager:\n" + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
