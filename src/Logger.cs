// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.IO;

namespace TaskbarFetch
{
    internal static class Logger
    {
        private static readonly object Sync = new object();
        public static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TaskbarFetch");
        public static readonly string LogFilePath = Path.Combine(LogDirectory, "TaskbarFetch.log");

        public static void Initialize()
        {
            try
            {
                Directory.CreateDirectory(LogDirectory);
                if (File.Exists(LogFilePath))
                {
                    var info = new FileInfo(LogFilePath);
                    if (info.Length > 2 * 1024 * 1024)
                    {
                        string old = Path.Combine(LogDirectory, "TaskbarFetch.old.log");
                        try { if (File.Exists(old)) File.Delete(old); } catch { }
                        try { File.Move(LogFilePath, old); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void Log(string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(LogDirectory);
                    File.AppendAllText(
                        LogFilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }

        public static void LogException(string context, Exception ex)
        {
            Log(context + ": " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
        }
    }
}
