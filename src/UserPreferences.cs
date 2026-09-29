// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using Microsoft.Win32;
using System.Windows.Forms;

namespace TaskbarFetch
{
    internal static class StartupManager
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "TaskbarFetch";

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                return key != null && key.GetValue(ValueName) != null;
            }
        }

        public static bool IsEnabledForCurrentExecutable()
        {
            string expected = Quote(Application.ExecutablePath);
            using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
            {
                if (key == null)
                    return false;
                string current = key.GetValue(ValueName) as string;
                return string.Equals(current, expected, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
            {
                if (key == null)
                    throw new InvalidOperationException("Could not open the current-user Run registry key.");

                if (enabled)
                    key.SetValue(ValueName, Quote(Application.ExecutablePath), RegistryValueKind.String);
                else
                    key.DeleteValue(ValueName, false);
            }
        }

        private static string Quote(string path)
        {
            return "\"" + path + "\"";
        }
    }

    internal static class UserSettingsManager
    {
        private const string SettingsKeyPath = @"Software\ThyannSeng\TaskbarFetch";
        private const string MoveAlreadyActiveValueName = "MoveAlreadyActiveWindowOnCrossMonitorClick";
        private const bool DefaultMoveAlreadyActiveWindowOnCrossMonitorClick = true;

        public static bool GetMoveAlreadyActiveWindowOnCrossMonitorClick()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(SettingsKeyPath, false))
                {
                    if (key == null)
                        return DefaultMoveAlreadyActiveWindowOnCrossMonitorClick;

                    object value = key.GetValue(MoveAlreadyActiveValueName);
                    if (value is int)
                        return (int)value != 0;
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to read user settings; using defaults", ex);
            }

            return DefaultMoveAlreadyActiveWindowOnCrossMonitorClick;
        }

        public static void SetMoveAlreadyActiveWindowOnCrossMonitorClick(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(SettingsKeyPath))
            {
                if (key == null)
                    throw new InvalidOperationException("Could not open the current-user TaskbarFetch settings key.");

                key.SetValue(
                    MoveAlreadyActiveValueName,
                    enabled ? 1 : 0,
                    RegistryValueKind.DWord);
            }
        }
    }
}
