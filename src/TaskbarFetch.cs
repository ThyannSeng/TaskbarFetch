// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.
//
// Original implementation for TaskbarFetch. No third-party application source
// code is incorporated into this file. Windows functionality is accessed
// through documented Win32 and accessibility APIs.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Accessibility;

[assembly: System.Reflection.AssemblyTitle("TaskbarFetch")]
[assembly: System.Reflection.AssemblyProduct("TaskbarFetch")]
[assembly: System.Reflection.AssemblyCompany("Thyann Seng")]
[assembly: System.Reflection.AssemblyCopyright("Copyright (c) 2026 Thyann Seng")]
[assembly: System.Reflection.AssemblyDescription("Move an existing app window to the monitor whose taskbar button was clicked.")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.0.0-beta.1")]

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

    internal sealed class TrayApplicationContext : ApplicationContext, IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _icon;
        private readonly ToolStripMenuItem _pauseItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly TaskbarFetchEngine _engine;
        private bool _disposed;

        public TrayApplicationContext()
        {
            _engine = new TaskbarFetchEngine();

            _icon = TryLoadApplicationIcon();

            var menu = new ContextMenuStrip();

            var statusItem = new ToolStripMenuItem("TaskbarFetch is active");
            statusItem.Enabled = false;
            statusItem.Name = "status";
            menu.Items.Add(statusItem);
            menu.Items.Add(new ToolStripSeparator());

            _pauseItem = new ToolStripMenuItem("Pause");
            _pauseItem.Click += delegate { TogglePause(menu); };
            menu.Items.Add(_pauseItem);

            _startupItem = new ToolStripMenuItem("Start with Windows");
            _startupItem.Checked = StartupManager.IsEnabledForCurrentExecutable();
            _startupItem.CheckOnClick = false;
            _startupItem.Click += delegate { ToggleStartup(); };
            menu.Items.Add(_startupItem);

            var logItem = new ToolStripMenuItem("Open log folder");
            logItem.Click += delegate { OpenLogFolder(); };
            menu.Items.Add(logItem);

            menu.Items.Add(new ToolStripSeparator());

            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += delegate
            {
                MessageBox.Show(
                    "TaskbarFetch " + GetInformationalVersion() + "\r\n" +
                    "Created by Thyann Seng\r\n\r\n" +
                    "Click an application's taskbar button on the monitor where you want that window to appear. " +
                    "If Windows shows thumbnail previews for a grouped app, choose the desired thumbnail.\r\n\r\n" +
                    "TaskbarFetch preserves normal minimize behavior when you click the taskbar button on the same monitor.",
                    "About TaskbarFetch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };
            menu.Items.Add(aboutItem);

            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += delegate
            {
                Logger.Log("Exit requested from tray menu.");
                ExitThread();
            };
            menu.Items.Add(exitItem);

            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = _icon;
            _notifyIcon.Text = "TaskbarFetch - active";
            _notifyIcon.Visible = true;
            _notifyIcon.ContextMenuStrip = menu;
            _notifyIcon.DoubleClick += delegate { TogglePause(menu); };

            // Install the global hooks only after WinForms has entered its message loop.
            // Both WH_MOUSE_LL and SetWinEventHook rely on that loop for delivery.
            Application.Idle += StartEngineOnFirstIdle;
        }

        private static string GetInformationalVersion()
        {
            var assembly = typeof(TrayApplicationContext).Assembly;
            var attribute = (System.Reflection.AssemblyInformationalVersionAttribute)
                Attribute.GetCustomAttribute(
                    assembly,
                    typeof(System.Reflection.AssemblyInformationalVersionAttribute));

            return attribute != null
                ? attribute.InformationalVersion
                : assembly.GetName().Version.ToString();
        }

        private void StartEngineOnFirstIdle(object sender, EventArgs e)
        {
            Application.Idle -= StartEngineOnFirstIdle;
            try
            {
                _engine.Start();
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to start hooks", ex);
                MessageBox.Show(
                    "TaskbarFetch could not start its Windows hooks.\r\n\r\n" + ex.Message +
                    "\r\n\r\nLog: " + Logger.LogFilePath,
                    "TaskbarFetch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                ExitThread();
            }
        }

        private static Icon TryLoadApplicationIcon()
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                    return icon;
            }
            catch { }
            return (Icon)SystemIcons.Application.Clone();
        }

        private void TogglePause(ContextMenuStrip menu)
        {
            try
            {
                bool newEnabled = !_engine.Enabled;
                _engine.Enabled = newEnabled;
                _pauseItem.Text = newEnabled ? "Pause" : "Resume";
                _notifyIcon.Text = newEnabled ? "TaskbarFetch - active" : "TaskbarFetch - paused";

                var status = menu.Items["status"] as ToolStripMenuItem;
                if (status != null)
                    status.Text = newEnabled ? "TaskbarFetch is active" : "TaskbarFetch is paused";

                Logger.Log(newEnabled ? "Resumed." : "Paused.");
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to toggle pause", ex);
                MessageBox.Show(ex.Message, "TaskbarFetch", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ToggleStartup()
        {
            try
            {
                bool enable = !StartupManager.IsEnabledForCurrentExecutable();
                StartupManager.SetEnabled(enable);
                _startupItem.Checked = StartupManager.IsEnabledForCurrentExecutable();
                Logger.Log(_startupItem.Checked ? "Startup enabled." : "Startup disabled.");
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to change startup setting", ex);
                MessageBox.Show(
                    "Could not change the startup setting.\r\n\r\n" + ex.Message,
                    "TaskbarFetch",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static void OpenLogFolder()
        {
            try
            {
                Directory.CreateDirectory(Logger.LogDirectory);
                Process.Start(new ProcessStartInfo
                {
                    FileName = Logger.LogDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to open log folder", ex);
            }
        }

        protected override void ExitThreadCore()
        {
            Dispose();
            base.ExitThreadCore();
        }

        public new void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try { Application.Idle -= StartEngineOnFirstIdle; } catch { }
            try { _engine.Dispose(); } catch { }
            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch { }
            try { _icon.Dispose(); } catch { }
            base.Dispose();
        }
    }

    internal sealed class TaskbarFetchEngine : IDisposable
    {
        private const int InitialEvaluationDelayMs = 180;
        private const int ForegroundEvaluationDelayMs = 70;
        private const int PendingTimeoutMs = 3500;

        private readonly object _sync = new object();
        private readonly NativeMethods.LowLevelMouseProc _mouseProc;
        private readonly NativeMethods.WinEventDelegate _winEventProc;
        private GCHandle _mouseProcHandle;
        private GCHandle _winEventProcHandle;
        private readonly System.Threading.Timer _evaluationTimer;
        private readonly AutoResetEvent _accessibilitySignal;
        private readonly Thread _accessibilityThread;

        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _winEventHook = IntPtr.Zero;
        private PendingClick _pending;
        private HitTestRequest _latestHitTestRequest;
        private int _nextPendingId;
        private int _evaluationInProgress;
        private volatile bool _accessibilityWorkerStop;
        private volatile bool _enabled;
        private bool _disposed;

        public TaskbarFetchEngine()
        {
            _mouseProc = MouseHookProc;
            _winEventProc = WinEventProc;
            _mouseProcHandle = GCHandle.Alloc(_mouseProc);
            _winEventProcHandle = GCHandle.Alloc(_winEventProc);
            _evaluationTimer = new System.Threading.Timer(EvaluationTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
            _accessibilitySignal = new AutoResetEvent(false);
            _accessibilityThread = new Thread(AccessibilityWorkerLoop);
            _accessibilityThread.IsBackground = true;
            _accessibilityThread.Name = "TaskbarFetch Accessibility";
            _accessibilityThread.SetApartmentState(ApartmentState.STA);
            _accessibilityThread.Start();
        }

        public bool Enabled
        {
            get { return _enabled; }
            set
            {
                if (_disposed)
                    return;

                if (value == _enabled)
                    return;

                if (value)
                    InstallHooks();
                else
                    RemoveHooks();
            }
        }

        public void Start()
        {
            if (!_enabled)
                InstallHooks();
        }

        private void InstallHooks()
        {
            if (_disposed)
                throw new ObjectDisposedException("TaskbarFetchEngine");

            if (_mouseHook != IntPtr.Zero || _winEventHook != IntPtr.Zero)
                RemoveHooks();

            IntPtr module = NativeMethods.GetModuleHandle(null);
            _mouseHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _mouseProc, module, 0);
            if (_mouseHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Could not install the mouse hook. Win32 error: " + error);
            }

            _winEventHook = NativeMethods.SetWinEventHook(
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                NativeMethods.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _winEventProc,
                0,
                0,
                NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

            if (_winEventHook == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                NativeMethods.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
                throw new InvalidOperationException("Could not install the foreground-window hook. Win32 error: " + error);
            }

            _enabled = true;
            Logger.Log("Hooks installed.");
        }

        private void RemoveHooks()
        {
            _enabled = false;
            ClearPending();

            if (_mouseHook != IntPtr.Zero)
            {
                try { NativeMethods.UnhookWindowsHookEx(_mouseHook); } catch { }
                _mouseHook = IntPtr.Zero;
            }

            if (_winEventHook != IntPtr.Zero)
            {
                try { NativeMethods.UnhookWinEvent(_winEventHook); } catch { }
                _winEventHook = IntPtr.Zero;
            }

            Logger.Log("Hooks removed.");
        }

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && _enabled)
                {
                    int message = wParam.ToInt32();
                    if (message == NativeMethods.WM_LBUTTONDOWN)
                    {
                        var data = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                            lParam,
                            typeof(NativeMethods.MSLLHOOKSTRUCT));
                        HandleLeftButtonDown(data.pt);
                    }
                    else if (message == NativeMethods.WM_LBUTTONUP)
                    {
                        HandleLeftButtonUp();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogException("Mouse hook callback error", ex);
            }

            return NativeMethods.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        private void HandleLeftButtonDown(NativeMethods.POINT point)
        {
            IntPtr taskbar = WindowUtilities.FindTaskbarAtPoint(point);
            if (taskbar == IntPtr.Zero)
                return;

            IntPtr targetMonitor = NativeMethods.MonitorFromPoint(point, NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (targetMonitor == IntPtr.Zero)
                return;

            IntPtr beforeWindow = NativeMethods.GetForegroundWindow();
            bool beforeWasMaximized = beforeWindow != IntPtr.Zero && NativeMethods.IsZoomed(beforeWindow);
            IntPtr beforeMonitor = beforeWindow != IntPtr.Zero
                ? NativeMethods.MonitorFromWindow(beforeWindow, NativeMethods.MONITOR_DEFAULTTONEAREST)
                : IntPtr.Zero;

            var pending = new PendingClick();
            pending.Id = Interlocked.Increment(ref _nextPendingId);
            pending.TargetMonitor = targetMonitor;
            pending.BeforeWindow = beforeWindow;
            pending.BeforeMonitor = beforeMonitor;
            pending.BeforeWasMaximized = beforeWasMaximized;
            pending.StartedUtc = DateTime.UtcNow;
            pending.HitKind = TaskbarHitKind.Unknown;

            lock (_sync)
            {
                _pending = pending;
                _latestHitTestRequest = new HitTestRequest
                {
                    PendingId = pending.Id,
                    Taskbar = taskbar,
                    Point = point
                };
            }
            _accessibilitySignal.Set();

        }

        private void AccessibilityWorkerLoop()
        {
            while (!_accessibilityWorkerStop)
            {
                _accessibilitySignal.WaitOne();
                if (_accessibilityWorkerStop)
                    break;

                HitTestRequest request = null;
                lock (_sync)
                {
                    if (_latestHitTestRequest != null)
                    {
                        request = _latestHitTestRequest;
                        _latestHitTestRequest = null;
                    }
                }

                if (request == null)
                    continue;

                TaskbarHitKind kind = AccessibilityHitTester.ClassifyTaskbarPoint(request.Taskbar, request.Point);
                bool clear = false;
                bool reevaluate = false;

                lock (_sync)
                {
                    if (_pending != null && _pending.Id == request.PendingId)
                    {
                        if (kind == TaskbarHitKind.ShellControl)
                        {
                            _pending = null;
                            clear = true;
                        }
                        else
                        {
                            _pending.HitKind = kind;
                            reevaluate = kind == TaskbarHitKind.AppButton;
                        }
                    }
                }

                if (clear)
                {
                    try { _evaluationTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
                    Logger.Log("Taskbar click classified as shell control; ignored.");
                }
                else if (reevaluate)
                {
                    ScheduleEvaluation(20);
                }
            }
        }

        private void HandleLeftButtonUp()
        {
            PendingClick pending = GetPending();
            if (pending == null)
                return;

            ScheduleEvaluation(InitialEvaluationDelayMs);
        }

        private void WinEventProc(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint idEventThread,
            uint eventTime)
        {
            try
            {
                if (!_enabled || eventType != NativeMethods.EVENT_SYSTEM_FOREGROUND)
                    return;

                PendingClick pending = GetPending();
                if (pending == null)
                    return;

                double ageMs = (DateTime.UtcNow - pending.StartedUtc).TotalMilliseconds;
                if (ageMs > PendingTimeoutMs)
                {
                    ClearPending();
                    return;
                }

                int delay = ageMs < InitialEvaluationDelayMs
                    ? (int)Math.Max(20, InitialEvaluationDelayMs - ageMs)
                    : ForegroundEvaluationDelayMs;
                ScheduleEvaluation(delay);
            }
            catch (Exception ex)
            {
                Logger.LogException("Foreground event callback error", ex);
            }
        }

        private void EvaluationTimerCallback(object state)
        {
            if (Interlocked.Exchange(ref _evaluationInProgress, 1) != 0)
                return;

            try
            {
                EvaluatePendingClick();
            }
            catch (Exception ex)
            {
                Logger.LogException("Pending click evaluation error", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _evaluationInProgress, 0);
            }
        }

        private void EvaluatePendingClick()
        {
            if (!_enabled || _disposed)
                return;

            PendingClick pending = GetPending();
            if (pending == null)
                return;

            double ageMs = (DateTime.UtcNow - pending.StartedUtc).TotalMilliseconds;
            if (ageMs > PendingTimeoutMs)
            {
                ClearPending();
                return;
            }

            // Important special case: clicking the taskbar button for the currently-active
            // window normally minimizes it. If that click happened on another monitor,
            // restore the same window and move it there. On the same monitor we preserve
            // the normal Windows minimize behavior.
            if (pending.BeforeWindow != IntPtr.Zero &&
                NativeMethods.IsWindow(pending.BeforeWindow) &&
                NativeMethods.IsIconic(pending.BeforeWindow))
            {
                IntPtr beforeMonitor = pending.BeforeMonitor != IntPtr.Zero
                    ? pending.BeforeMonitor
                    : NativeMethods.MonitorFromWindow(
                        pending.BeforeWindow,
                        NativeMethods.MONITOR_DEFAULTTONEAREST);

                if (beforeMonitor == pending.TargetMonitor)
                {
                    Logger.Log("Same-monitor taskbar minimize preserved.");
                    ClearPending();
                    return;
                }

                if (WindowUtilities.IsCandidateApplicationWindow(pending.BeforeWindow))
                {
                    bool moved = WindowMover.MoveWindowToMonitor(
                        pending.BeforeWindow,
                        pending.TargetMonitor,
                        pending.BeforeWasMaximized);
                    Logger.Log(moved ? "Moved previously-active minimized window." : "Move of minimized window was not needed/failed.");
                    ClearPending();
                    return;
                }
            }

            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero && WindowUtilities.IsCandidateApplicationWindow(foreground))
            {
                IntPtr foregroundMonitor = NativeMethods.MonitorFromWindow(
                    foreground,
                    NativeMethods.MONITOR_DEFAULTTONEAREST);

                if (foreground != pending.BeforeWindow)
                {
                    if (foregroundMonitor != pending.TargetMonitor)
                    {
                        bool moved = WindowMover.MoveWindowToMonitor(
                            foreground,
                            pending.TargetMonitor,
                            NativeMethods.IsZoomed(foreground));
                        Logger.Log(moved ? "Moved newly-foreground window." : "Foreground move was not needed/failed.");
                    }
                    ClearPending();
                    return;
                }

                // If the same window stayed foreground, this can be an app that does not
                // minimize on a taskbar click. Move it directly only when accessibility
                // positively identified an app button and the process has one normal
                // top-level window. When multiple windows exist we keep the target pending
                // so the user's thumbnail selection decides which HWND is moved.
                if (foregroundMonitor != pending.TargetMonitor &&
                    pending.HitKind == TaskbarHitKind.AppButton &&
                    ageMs >= 260)
                {
                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(foreground, out processId);
                    int topLevelCount = WindowUtilities.CountCandidateWindowsForProcess(processId);
                    if (topLevelCount <= 1)
                    {
                        bool moved = WindowMover.MoveWindowToMonitor(
                            foreground,
                            pending.TargetMonitor,
                            NativeMethods.IsZoomed(foreground));
                        Logger.Log(moved ? "Moved same foreground single-window app." : "Same-window move was not needed/failed.");
                        ClearPending();
                        return;
                    }
                }
            }

            // Keep the target alive briefly for grouped-taskbar thumbnail selection.
            int remaining = PendingTimeoutMs - (int)ageMs;
            if (remaining > 0)
                ScheduleEvaluation(remaining + 20);
            else
                ClearPending();
        }

        private PendingClick GetPending()
        {
            lock (_sync)
            {
                return _pending;
            }
        }

        private void ClearPending()
        {
            lock (_sync)
            {
                _pending = null;
                _latestHitTestRequest = null;
            }
            try { _evaluationTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
        }

        private void ScheduleEvaluation(int delayMs)
        {
            if (_disposed)
                return;
            if (delayMs < 1)
                delayMs = 1;
            try { _evaluationTimer.Change(delayMs, Timeout.Infinite); } catch (ObjectDisposedException) { }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            RemoveHooks();
            _accessibilityWorkerStop = true;
            try { _accessibilitySignal.Set(); } catch { }
            try
            {
                if (_accessibilityThread != null && _accessibilityThread.IsAlive)
                    _accessibilityThread.Join(500);
            }
            catch { }
            try { _accessibilitySignal.Dispose(); } catch { }
            try { _evaluationTimer.Dispose(); } catch { }
            try { if (_mouseProcHandle.IsAllocated) _mouseProcHandle.Free(); } catch { }
            try { if (_winEventProcHandle.IsAllocated) _winEventProcHandle.Free(); } catch { }
        }

        private sealed class HitTestRequest
        {
            public int PendingId;
            public IntPtr Taskbar;
            public NativeMethods.POINT Point;
        }

        private sealed class PendingClick
        {
            public int Id;
            public IntPtr TargetMonitor;
            public IntPtr BeforeWindow;
            public IntPtr BeforeMonitor;
            public bool BeforeWasMaximized;
            public DateTime StartedUtc;
            public TaskbarHitKind HitKind;
        }
    }

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

        public static TaskbarHitKind ClassifyTaskbarPoint(IntPtr taskbar, NativeMethods.POINT point)
        {
            try
            {
                if (WindowUtilities.IsPointInNotificationArea(taskbar, point))
                    return TaskbarHitKind.ShellControl;

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
            Rectangle newRect = MapRectangle(oldRect, srcWork, dstWork);

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

        internal static Rectangle MapRectangle(Rectangle window, Rectangle sourceWork, Rectangle targetWork)
        {
            if (sourceWork.Width <= 0 || sourceWork.Height <= 0 ||
                targetWork.Width <= 0 || targetWork.Height <= 0)
                return window;

            double xRatio = (double)(window.Left - sourceWork.Left) / sourceWork.Width;
            double yRatio = (double)(window.Top - sourceWork.Top) / sourceWork.Height;
            double widthRatio = (double)window.Width / sourceWork.Width;
            double heightRatio = (double)window.Height / sourceWork.Height;

            int width = (int)Math.Round(widthRatio * targetWork.Width);
            int height = (int)Math.Round(heightRatio * targetWork.Height);

            int minWidth = Math.Min(120, targetWork.Width);
            int minHeight = Math.Min(80, targetWork.Height);
            width = Math.Max(minWidth, Math.Min(width, targetWork.Width));
            height = Math.Max(minHeight, Math.Min(height, targetWork.Height));

            int left = targetWork.Left + (int)Math.Round(xRatio * targetWork.Width);
            int top = targetWork.Top + (int)Math.Round(yRatio * targetWork.Height);

            // Keep the window fully reachable on the destination work area.
            if (left < targetWork.Left)
                left = targetWork.Left;
            if (top < targetWork.Top)
                top = targetWork.Top;
            if (left + width > targetWork.Right)
                left = targetWork.Right - width;
            if (top + height > targetWork.Bottom)
                top = targetWork.Bottom - height;

            return new Rectangle(left, top, width, height);
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

        private static readonly HashSet<string> NotificationAreaClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TrayNotifyWnd",
            "NotifyIconOverflowWindow",
            "TopLevelWindowForOverflowXamlIsland",
            "ClockButton"
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

        public static bool IsPointInNotificationArea(IntPtr taskbar, NativeMethods.POINT point)
        {
            bool found = false;
            NativeMethods.EnumChildWindows(taskbar, delegate(IntPtr child, IntPtr lParam)
            {
                string className = GetClassName(child);
                if (NotificationAreaClasses.Contains(className) && PointInsideWindow(child, point))
                {
                    found = true;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
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

    internal sealed class MonitorInformation
    {
        public Rectangle MonitorArea;
        public Rectangle WorkArea;
        public bool IsPrimary;
    }

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

    internal static class NativeMethods
    {
        public const int WH_MOUSE_LL = 14;
        public const int WM_LBUTTONDOWN = 0x0201;
        public const int WM_LBUTTONUP = 0x0202;

        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        public const uint MONITOR_DEFAULTTONEAREST = 0x00000002;
        public const uint MONITORINFOF_PRIMARY = 0x00000001;

        public const int GA_ROOT = 2;

        public const int SW_SHOW = 5;
        public const int SW_MINIMIZE = 6;
        public const int SW_SHOWMINNOACTIVE = 7;
        public const int SW_SHOWNA = 8;
        public const int SW_RESTORE = 9;
        public const int SW_SHOWMAXIMIZED = 3;
        public const int SW_SHOWMINIMIZED = 2;
        public const int SW_MAXIMIZE = 3;

        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOOWNERZORDER = 0x0200;

        public const long WS_CHILD = 0x40000000L;
        public const long WS_EX_TOOLWINDOW = 0x00000080L;

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;

        public const int DWMWA_CLOAKED = 14;

        public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        public delegate void WinEventDelegate(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint idEventThread,
            uint eventTime);
        [return: MarshalAs(UnmanagedType.Bool)]
        public delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public Rectangle ToRectangle()
            {
                return Rectangle.FromLTRB(Left, Top, Right, Bottom);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelMouseProc lpfn,
            IntPtr hMod,
            uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWinEventHook(
            uint eventMin,
            uint eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr FindWindowEx(
            IntPtr hwndParent,
            IntPtr hwndChildAfter,
            string lpszClass,
            string lpszWindow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        public static long GetWindowStyle(IntPtr hwnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, GWL_STYLE).ToInt64()
                : GetWindowLong32(hwnd, GWL_STYLE);
        }

        public static long GetWindowExStyle(IntPtr hwnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, GWL_EXSTYLE).ToInt64()
                : GetWindowLong32(hwnd, GWL_EXSTYLE);
        }

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(
            IntPtr hwnd,
            int dwAttribute,
            out int pvAttribute,
            int cbAttribute);

        [DllImport("oleacc.dll")]
        public static extern int AccessibleObjectFromPoint(
            POINT ptScreen,
            [MarshalAs(UnmanagedType.Interface)] out IAccessible ppacc,
            [MarshalAs(UnmanagedType.Struct)] out object pvarChild);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiFlag);

        public static void TryEnablePerMonitorV2DpiAwareness()
        {
            try
            {
                // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
                SetProcessDpiAwarenessContext(new IntPtr(-4));
            }
            catch (EntryPointNotFoundException) { }
            catch { }
        }

        public static MonitorInformation GetMonitorInformation(IntPtr monitor)
        {
            if (monitor == IntPtr.Zero)
                return null;

            var info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(monitor, ref info))
                return null;

            return new MonitorInformation
            {
                MonitorArea = info.rcMonitor.ToRectangle(),
                WorkArea = info.rcWork.ToRectangle(),
                IsPrimary = (info.dwFlags & MONITORINFOF_PRIMARY) != 0
            };
        }
    }
}
