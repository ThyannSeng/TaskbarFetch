// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Accessibility;

namespace TaskbarFetch
{
    internal enum TaskbarHitKind
    {
        Unknown,
        AppButton,
        ShellControl
    }

    internal static class AccessibilityHitTester
    {
        private const int ROLE_SYSTEM_PUSHBUTTON = 0x2B;
        private const int ROLE_SYSTEM_LISTITEM = 0x22;
        private const int ROLE_SYSTEM_BUTTONDROPDOWN = 0x38;
        private const int ROLE_SYSTEM_BUTTONMENU = 0x39;
        private const int ROLE_SYSTEM_BUTTONDROPDOWNGRID = 0x3A;

        public static TaskbarHitKind ClassifyTaskbarPoint(NativeMethods.POINT point)
        {
            try
            {
                IAccessible accessible;
                object child;
                int hr = NativeMethods.AccessibleObjectFromPoint(point, out accessible, out child);
                if (hr != 0 || accessible == null)
                    return TaskbarHitKind.Unknown;

                try
                {
                    string name = SafeGetName(accessible, child);
                    string defaultAction = SafeGetDefaultAction(accessible, child);
                    int role = SafeGetRole(accessible, child);

                    if (IsKnownShellControlName(name))
                        return TaskbarHitKind.ShellControl;

                    string lowerName = (name ?? string.Empty).Trim().ToLowerInvariant();
                    string lowerAction = (defaultAction ?? string.Empty).Trim().ToLowerInvariant();

                    if (lowerName.Contains("running window") ||
                        lowerName.Contains("running windows") ||
                        lowerAction.Contains("switch"))
                        return TaskbarHitKind.AppButton;

                    if (role == ROLE_SYSTEM_PUSHBUTTON ||
                        role == ROLE_SYSTEM_LISTITEM ||
                        role == ROLE_SYSTEM_BUTTONDROPDOWN ||
                        role == ROLE_SYSTEM_BUTTONMENU ||
                        role == ROLE_SYSTEM_BUTTONDROPDOWNGRID)
                    {
                        // For a taskbar point, an accessible push/list button that is not
                        // one of the known shell controls is very likely an app button.
                        return TaskbarHitKind.AppButton;
                    }

                    return TaskbarHitKind.Unknown;
                }
                finally
                {
                    try
                    {
                        if (Marshal.IsComObject(accessible))
                            Marshal.ReleaseComObject(accessible);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("Accessibility taskbar hit-test failed", ex);
                return TaskbarHitKind.Unknown;
            }
        }

        private static string SafeGetName(IAccessible accessible, object child)
        {
            try { return accessible.get_accName(child); } catch { return null; }
        }

        private static string SafeGetDefaultAction(IAccessible accessible, object child)
        {
            try { return accessible.get_accDefaultAction(child); } catch { return null; }
        }

        private static int SafeGetRole(IAccessible accessible, object child)
        {
            try
            {
                object role = accessible.get_accRole(child);
                if (role == null)
                    return -1;
                return Convert.ToInt32(role);
            }
            catch { return -1; }
        }

        private static bool IsKnownShellControlName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            string n = name.Trim().ToLowerInvariant();

            string[] exactOrPrefix =
            {
                "start",
                "search",
                "task view",
                "widgets",
                "copilot",
                "chat",
                "show hidden icons",
                "notification center",
                "quick settings",
                "system tray",
                "show desktop",
                "touch keyboard",
                "virtual touchpad",
                "pen menu",
                "input indicator",
                "date and time",
                "clock"
            };

            for (int i = 0; i < exactOrPrefix.Length; i++)
            {
                string item = exactOrPrefix[i];
                if (n == item || n.StartsWith(item + " ") || n.StartsWith(item + ","))
                    return true;
            }

            // Typical Windows 11 tray accessible names.
            if (n.Contains("battery") ||
                n.Contains("volume") ||
                n.Contains("speakers") ||
                n.Contains("microphone") ||
                n.Contains("network") ||
                n.Contains("internet access"))
                return true;

            return false;
        }
    }
}
