// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Threading;
using System.Windows.Forms;

namespace TaskbarFetch
{
    internal static class Program
    {
        private const string MutexName = @"Local\TaskbarFetch.Singleton.v1";

        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "TaskbarFetch is already running. Look for its icon in the system tray.",
                        "TaskbarFetch",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                Logger.Initialize();
                Logger.Log("TaskbarFetch starting.");
                NativeMethods.TryEnablePerMonitorV2DpiAwareness();

                Application.ThreadException += delegate(object sender, System.Threading.ThreadExceptionEventArgs e)
                {
                    Logger.LogException("UI thread exception", e.Exception);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    var exception = e.ExceptionObject as Exception;
                    if (exception != null)
                        Logger.LogException("Unhandled exception", exception);
                    else
                        Logger.Log("Unhandled non-Exception object.");
                };

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                try
                {
                    using (var context = new TrayApplicationContext())
                    {
                        Application.Run(context);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogException("Fatal startup/runtime error", ex);
                    MessageBox.Show(
                        "TaskbarFetch encountered an error and must close.\r\n\r\n" +
                        ex.Message + "\r\n\r\nLog: " + Logger.LogFilePath,
                        "TaskbarFetch",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                finally
                {
                    Logger.Log("TaskbarFetch stopped.");
                    try { mutex.ReleaseMutex(); } catch { }
                }
            }
        }
    }
}
