// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace TaskbarFetch
{
    internal static class WindowMover
    {
        public static bool MoveWindowToMonitor(IntPtr hwnd, IntPtr targetMonitor, bool shouldBeMaximized)
        {
            if (hwnd == IntPtr.Zero || targetMonitor == IntPtr.Zero)
                return false;
            if (!NativeMethods.IsWindow(hwnd))
                return false;

            IntPtr sourceMonitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (sourceMonitor == IntPtr.Zero)
                return false;

            if (sourceMonitor == targetMonitor)
            {
                WindowUtilities.ActivateWindow(hwnd);
                return false;
            }

            var sourceInfo = NativeMethods.GetMonitorInformation(sourceMonitor);
            var targetInfo = NativeMethods.GetMonitorInformation(targetMonitor);
            if (sourceInfo == null || targetInfo == null)
                return false;

            bool wasMinimized = NativeMethods.IsIconic(hwnd);
            bool wasMaximized = shouldBeMaximized || NativeMethods.IsZoomed(hwnd);

            // Get the actual restored rectangle in screen coordinates. Microsoft documents
            // WINDOWPLACEMENT.rcNormalPosition as workspace coordinates, so we intentionally
            // do not feed that directly into SetWindowPos.
            if (wasMinimized)
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            if (NativeMethods.IsZoomed(hwnd))
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);

            NativeMethods.RECT rect;
            if (!NativeMethods.GetWindowRect(hwnd, out rect))
                return false;

            Rectangle srcWork = sourceInfo.WorkArea;
            Rectangle dstWork = targetInfo.WorkArea;
            Rectangle oldRect = rect.ToRectangle();
            Rectangle newRect = WindowGeometry.MapRectangle(oldRect, srcWork, dstWork);

            bool positioned = NativeMethods.SetWindowPos(
                hwnd,
                IntPtr.Zero,
                newRect.Left,
                newRect.Top,
                newRect.Width,
                newRect.Height,
                NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOOWNERZORDER |
                NativeMethods.SWP_SHOWWINDOW);

            if (!positioned)
            {
                int error = Marshal.GetLastWin32Error();
                Logger.Log("SetWindowPos failed. Win32 error=" + error + ", hwnd=" + hwnd.ToInt64());
                return false;
            }

            if (wasMaximized)
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MAXIMIZE);
            else
                NativeMethods.ShowWindow(hwnd, NativeMethods.SW_SHOW);

            WindowUtilities.ActivateWindow(hwnd);
            return true;
        }

    }

    internal static class WindowUtilities
    {
        private static readonly HashSet<string> ExcludedWindowClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Shell_TrayWnd",
            "Shell_SecondaryTrayWnd",
            "Progman",
            "WorkerW",
            "TaskListThumbnailWnd",
            "TaskListOverlayWnd",
            "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland",
            "XamlExplorerHostIslandWindow",
            "Xaml_WindowedPopupClass",
            "ControlCenterWindow",
            "Windows.UI.Core.CoreWindow"
        };

        private static readonly HashSet<string> ExcludedShellProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "StartMenuExperienceHost",
            "SearchHost",
            "SearchApp",
            "ShellExperienceHost",
            "TextInputHost",
            "Widgets",
            "WidgetService"
        };

        public static IntPtr FindTaskbarAtPoint(NativeMethods.POINT point)
        {
            IntPtr primary = NativeMethods.FindWindow("Shell_TrayWnd", null);
            if (primary != IntPtr.Zero && PointInsideWindow(primary, point))
                return primary;

            IntPtr after = IntPtr.Zero;
            while (true)
            {
                IntPtr secondary = NativeMethods.FindWindowEx(
                    IntPtr.Zero,
                    after,
                    "Shell_SecondaryTrayWnd",
                    null);
                if (secondary == IntPtr.Zero)
                    break;
                if (PointInsideWindow(secondary, point))
                    return secondary;
                after = secondary;
            }

            return IntPtr.Zero;
        }

        public static bool PointInsideWindow(IntPtr hwnd, NativeMethods.POINT point)
        {
            NativeMethods.RECT rect;
            if (!NativeMethods.GetWindowRect(hwnd, out rect))
                return false;
            return point.X >= rect.Left && point.X < rect.Right &&
                   point.Y >= rect.Top && point.Y < rect.Bottom;
        }

        public static bool IsCandidateApplicationWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd) || !NativeMethods.IsWindowVisible(hwnd))
                return false;

            if (NativeMethods.GetAncestor(hwnd, NativeMethods.GA_ROOT) != hwnd)
                return false;

            string className = GetClassName(hwnd);
            if (ExcludedWindowClasses.Contains(className))
                return false;

            long style = NativeMethods.GetWindowStyle(hwnd);
            long exStyle = NativeMethods.GetWindowExStyle(hwnd);
            if ((style & NativeMethods.WS_CHILD) != 0)
                return false;
            if ((exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0)
                return false;

            if (IsCloaked(hwnd))
                return false;

            if (IsExcludedShellProcess(hwnd))
                return false;

            NativeMethods.RECT rect;
            if (!NativeMethods.GetWindowRect(hwnd, out rect))
                return false;
            if (rect.Right - rect.Left < 40 || rect.Bottom - rect.Top < 40)
                return false;

            int titleLength = NativeMethods.GetWindowTextLength(hwnd);
            if (titleLength > 0)
                return true;

            // Some valid app windows can temporarily have an empty caption.
            // Accept common app-window classes even in that state.
            if (className.StartsWith("Chrome_WidgetWin_", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("MozillaWindowClass", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("CASCADIA_HOSTING_WINDOW_CLASS", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("ConsoleWindowClass", StringComparison.OrdinalIgnoreCase) ||
                className.Equals("UnityWndClass", StringComparison.OrdinalIgnoreCase) ||
                className.StartsWith("SDL_", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static bool IsExcludedShellProcess(IntPtr hwnd)
        {
            try
            {
                uint processId;
                NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
                if (processId == 0)
                    return false;

                using (var process = Process.GetProcessById(unchecked((int)processId)))
                {
                    return ExcludedShellProcesses.Contains(process.ProcessName);
                }
            }
            catch
            {
                return false;
            }
        }

        public static int CountCandidateWindowsForProcess(uint processId)
        {
            int count = 0;
            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                uint pid;
                NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
                if (pid == processId && IsCandidateApplicationWindow(hwnd))
                    count++;
                return true;
            }, IntPtr.Zero);
            return count;
        }

        public static int CountCandidateWindowsForTaskbarGroup(IntPtr hwnd)
        {
            if (IsFileExplorerWindow(hwnd))
                return CountCandidateFileExplorerWindows();

            uint processId;
            NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
            return processId == 0 ? 0 : CountCandidateWindowsForProcess(processId);
        }

        public static bool AreInSameApplicationGroup(IntPtr first, IntPtr second)
        {
            if (first == IntPtr.Zero || second == IntPtr.Zero ||
                !NativeMethods.IsWindow(first) || !NativeMethods.IsWindow(second))
                return false;

            if (IsFileExplorerWindow(first) && IsFileExplorerWindow(second))
                return true;

            uint firstProcessId;
            uint secondProcessId;
            NativeMethods.GetWindowThreadProcessId(first, out firstProcessId);
            NativeMethods.GetWindowThreadProcessId(second, out secondProcessId);
            return firstProcessId != 0 && firstProcessId == secondProcessId;
        }

        public static bool IsTaskbarThumbnailPreviewVisible()
        {
            bool found = false;
            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                if (!NativeMethods.IsWindowVisible(hwnd))
                    return true;

                string className = GetClassName(hwnd);
                if (className.Equals("TaskListThumbnailWnd", StringComparison.OrdinalIgnoreCase) ||
                    className.Equals("TaskListOverlayWnd", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    return false;
                }

                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static int CountCandidateFileExplorerWindows()
        {
            int count = 0;
            NativeMethods.EnumWindows(delegate(IntPtr hwnd, IntPtr lParam)
            {
                if (IsFileExplorerWindow(hwnd) && IsCandidateApplicationWindow(hwnd))
                    count++;
                return true;
            }, IntPtr.Zero);
            return count;
        }

        private static bool IsFileExplorerWindow(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !NativeMethods.IsWindow(hwnd))
                return false;

            string className = GetClassName(hwnd);
            return className.Equals("CabinetWClass", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("ExploreWClass", StringComparison.OrdinalIgnoreCase);
        }

        public static void ActivateWindow(IntPtr hwnd)
        {
            try
            {
                if (NativeMethods.IsIconic(hwnd))
                    NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
                NativeMethods.BringWindowToTop(hwnd);
                NativeMethods.SetForegroundWindow(hwnd);
            }
            catch (Exception ex)
            {
                Logger.LogException("ActivateWindow failed", ex);
            }
        }

        public static string GetClassName(IntPtr hwnd)
        {
            var sb = new StringBuilder(256);
            int length = NativeMethods.GetClassName(hwnd, sb, sb.Capacity);
            return length > 0 ? sb.ToString() : string.Empty;
        }

        private static bool IsCloaked(IntPtr hwnd)
        {
            try
            {
                int cloaked;
                int hr = NativeMethods.DwmGetWindowAttribute(
                    hwnd,
                    NativeMethods.DWMWA_CLOAKED,
                    out cloaked,
                    sizeof(int));
                return hr == 0 && cloaked != 0;
            }
            catch
            {
                return false;
            }
        }
    }

    internal sealed class MonitorInformation
    {
        public Rectangle MonitorArea;
        public Rectangle WorkArea;
        public bool IsPrimary;
    }
}
