// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace TaskbarFetch
{
    internal sealed class TaskbarFetchEngine : IDisposable
    {
        private const int InitialEvaluationDelayMs = 180;
        private const int ForegroundEvaluationDelayMs = 70;
        private const int SameForegroundMoveDelayMs = 260;
        private const int GroupedWindowGraceMs = 350;
        private const int PendingEvaluationIntervalMs = 100;
        private const int PendingTimeoutMs = 3500;

        private readonly object _sync = new object();
        private readonly PendingClickStore<PendingClick> _pendingClicks =
            new PendingClickStore<PendingClick>(pending => pending.Clone());
        private readonly NativeMethods.LowLevelMouseProc _mouseProc;
        private readonly NativeMethods.WinEventDelegate _winEventProc;
        private GCHandle _mouseProcHandle;
        private GCHandle _winEventProcHandle;
        private readonly System.Threading.Timer _evaluationTimer;
        private readonly AutoResetEvent _accessibilitySignal;
        private readonly Thread _accessibilityThread;

        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _winEventHook = IntPtr.Zero;
        private HitTestRequest _latestHitTestRequest;
        private int _nextPendingId;
        private int _evaluationInProgress;
        private int _evaluationRequested;
        private volatile bool _accessibilityWorkerStop;
        private volatile bool _enabled;
        private volatile bool _moveAlreadyActiveWindowOnCrossMonitorClick;
        private volatile bool _disposed;

        public bool MoveAlreadyActiveWindowOnCrossMonitorClick
        {
            get { return _moveAlreadyActiveWindowOnCrossMonitorClick; }
            set { _moveAlreadyActiveWindowOnCrossMonitorClick = value; }
        }

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
                _pendingClicks.Replace(pending.Id, pending);
                _latestHitTestRequest = new HitTestRequest
                {
                    PendingId = pending.Id,
                    Point = point
                };
            }
            Logger.Log(
                "Taskbar click started; pendingId=" + pending.Id +
                ", targetMonitor=" + targetMonitor.ToInt64() +
                ", priorWindow=" + beforeWindow.ToInt64() + ".");
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

                TaskbarHitKind kind = AccessibilityHitTester.ClassifyTaskbarPoint(request.Point);
                bool clear = false;
                bool reevaluate = false;

                lock (_sync)
                {
                    if (kind == TaskbarHitKind.ShellControl)
                    {
                        clear = TryClaimPendingLocked(request.PendingId);
                    }
                    else
                    {
                        bool updated = _pendingClicks.TryUpdate(
                            request.PendingId,
                            pending => pending.HitKind = kind);
                        reevaluate = updated && kind == TaskbarHitKind.AppButton;
                    }
                }

                Logger.Log("Taskbar point classified; pendingId=" + request.PendingId + ", hit=" + kind + ".");

                if (clear)
                {
                    Logger.Log("Taskbar shell control recognized by its accessible name; click ignored.");
                }
                else if (reevaluate)
                {
                    ScheduleEvaluation(20, request.PendingId);
                }
            }
        }

        private void HandleLeftButtonUp()
        {
            PendingClick pending = GetPending();
            if (pending == null)
                return;

            ScheduleEvaluation(InitialEvaluationDelayMs, pending.Id);
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
                    TryClaimPending(pending.Id);
                    return;
                }

                int delay = ageMs < InitialEvaluationDelayMs
                    ? (int)Math.Max(20, InitialEvaluationDelayMs - ageMs)
                    : ForegroundEvaluationDelayMs;
                ScheduleEvaluation(delay, pending.Id);
            }
            catch (Exception ex)
            {
                Logger.LogException("Foreground event callback error", ex);
            }
        }

        private void EvaluationTimerCallback(object state)
        {
            if (Interlocked.CompareExchange(ref _evaluationInProgress, 1, 0) != 0)
            {
                // A foreground notification can arrive while another evaluation is
                // inspecting Explorer's windows. Queue one more pass instead of losing
                // the notification and waiting for the pending-click timeout.
                Interlocked.Exchange(ref _evaluationRequested, 1);
                return;
            }

            try
            {
                do
                {
                    Interlocked.Exchange(ref _evaluationRequested, 0);
                    EvaluatePendingClick();
                }
                while (Interlocked.Exchange(ref _evaluationRequested, 0) != 0 &&
                       _enabled &&
                       !_disposed);
            }
            catch (Exception ex)
            {
                Logger.LogException("Pending click evaluation error", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _evaluationInProgress, 0);
                if (Interlocked.Exchange(ref _evaluationRequested, 0) != 0)
                    ScheduleEvaluation(1);
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
                if (TryClaimPending(pending.Id))
                {
                    Logger.Log(
                        "Taskbar click expired without a resolved window; pendingId=" + pending.Id +
                        ", hit=" + pending.HitKind +
                        ", thumbnailPreviewSeen=" + pending.SawThumbnailPreview + ".");
                }
                return;
            }

            // A grouped app can briefly foreground its most-recent window before the
            // thumbnail picker is ready. While the picker is visible, keep the click
            // pending so the window the user actually chooses becomes the target.
            if (pending.HitKind != TaskbarHitKind.ShellControl &&
                WindowUtilities.IsTaskbarThumbnailPreviewVisible())
            {
                pending.SawThumbnailPreview = true;
                if (!pending.ThumbnailPreviewWaitLogged)
                {
                    pending.ThumbnailPreviewWaitLogged = true;
                    Logger.Log("Taskbar thumbnail picker detected; waiting for the selected window; pendingId=" + pending.Id + ".");
                }

                if (!TryUpdateEvaluationState(pending))
                    return;

                ScheduleEvaluation(
                    Math.Min(PendingEvaluationIntervalMs, Math.Max(1, PendingTimeoutMs - (int)ageMs)),
                    pending.Id);
                return;
            }

            IntPtr foreground = NativeMethods.GetForegroundWindow();
            bool foregroundIsCandidate = foreground != IntPtr.Zero &&
                WindowUtilities.IsCandidateApplicationWindow(foreground);
            bool foregroundIsAnotherWindowInSameGroup =
                foregroundIsCandidate &&
                foreground != pending.BeforeWindow &&
                pending.BeforeWindow != IntPtr.Zero &&
                WindowUtilities.AreInSameApplicationGroup(pending.BeforeWindow, foreground);

            if (foregroundIsAnotherWindowInSameGroup)
            {
                int groupWindowCount = WindowUtilities.CountCandidateWindowsForTaskbarGroup(foreground);
                if (WaitForGroupedWindowActivation(pending, groupWindowCount))
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
                    if (!TryClaimPending(pending.Id))
                        return;

                    Logger.Log("Same-monitor taskbar minimize preserved; pendingId=" + pending.Id + ".");
                    return;
                }

                if (!foregroundIsAnotherWindowInSameGroup &&
                    WindowUtilities.IsCandidateApplicationWindow(pending.BeforeWindow))
                {
                    if (!TryClaimPending(pending.Id))
                        return;

                    bool moved = WindowMover.MoveWindowToMonitor(
                        pending.BeforeWindow,
                        pending.TargetMonitor,
                        pending.BeforeWasMaximized);
                    Logger.Log(
                        (moved ? "Moved previously-active minimized window." : "Move of minimized window was not needed/failed.") +
                        " pendingId=" + pending.Id + ".");
                    return;
                }
            }

            if (foregroundIsCandidate)
            {
                IntPtr foregroundMonitor = NativeMethods.MonitorFromWindow(
                    foreground,
                    NativeMethods.MONITOR_DEFAULTTONEAREST);

                if (foreground != pending.BeforeWindow)
                {
                    int candidateWindowCount = WindowUtilities.CountCandidateWindowsForTaskbarGroup(foreground);
                    if (WaitForGroupedWindowActivation(pending, candidateWindowCount))
                        return;

                    if (!TryClaimPending(pending.Id))
                        return;

                    bool moved = false;
                    if (foregroundMonitor != pending.TargetMonitor)
                    {
                        moved = WindowMover.MoveWindowToMonitor(
                            foreground,
                            pending.TargetMonitor,
                            NativeMethods.IsZoomed(foreground));
                    }
                    Logger.Log(
                        "Foreground selection resolved; pendingId=" + pending.Id +
                        ", window=" + foreground.ToInt64() +
                        ", candidateWindows=" + candidateWindowCount +
                        ", moved=" + moved + ".");
                    return;
                }

                // If the same window stayed foreground, this can be an app that does not
                // minimize on a taskbar click. If a thumbnail picker was visible, its
                // dismissal confirms the user's choice even when that choice is the same
                // window. Otherwise, keep a multi-window app pending to avoid guessing.
                if (MoveAlreadyActiveWindowOnCrossMonitorClick &&
                    foregroundMonitor != pending.TargetMonitor &&
                    (pending.HitKind == TaskbarHitKind.AppButton || pending.SawThumbnailPreview))
                {
                    if (ageMs < SameForegroundMoveDelayMs)
                    {
                        int delayMs = Math.Max(
                            1,
                            (int)Math.Ceiling(SameForegroundMoveDelayMs - ageMs));
                        ScheduleEvaluation(delayMs, pending.Id);
                        return;
                    }

                    int topLevelCount = WindowUtilities.CountCandidateWindowsForTaskbarGroup(foreground);
                    if (topLevelCount <= 1 || pending.SawThumbnailPreview)
                    {
                        if (!TryClaimPending(pending.Id))
                            return;

                        bool moved = WindowMover.MoveWindowToMonitor(
                            foreground,
                            pending.TargetMonitor,
                            NativeMethods.IsZoomed(foreground));
                        Logger.Log(
                            "Same-foreground selection resolved; pendingId=" + pending.Id +
                            ", window=" + foreground.ToInt64() +
                            ", candidateWindows=" + topLevelCount +
                            ", moved=" + moved + ".");
                        return;
                    }

                    if (!pending.GroupedWindowGraceLogged)
                    {
                        pending.GroupedWindowGraceLogged = true;
                        Logger.Log(
                            "Same foreground belongs to a multi-window app; waiting for thumbnail selection; pendingId=" +
                            pending.Id + ", candidateWindows=" + topLevelCount + ".");
                    }

                    if (!TryUpdateEvaluationState(pending))
                        return;
                }
            }

            // Poll only while this taskbar click is unresolved. This also catches a
            // thumbnail picker that appears just after the initial foreground change.
            int remaining = PendingTimeoutMs - (int)ageMs;
            if (remaining > 0)
                ScheduleEvaluation(Math.Min(PendingEvaluationIntervalMs, remaining), pending.Id);
            else if (TryClaimPending(pending.Id))
            {
                Logger.Log("Taskbar click timed out; pendingId=" + pending.Id + ", hit=" + pending.HitKind + ".");
            }
        }

        private bool WaitForGroupedWindowActivation(PendingClick pending, int candidateWindowCount)
        {
            if (pending.SawThumbnailPreview || candidateWindowCount <= 1)
                return false;

            if (pending.GroupedWindowGraceUntilUtc == DateTime.MinValue)
            {
                pending.GroupedWindowGraceUntilUtc = DateTime.UtcNow.AddMilliseconds(GroupedWindowGraceMs);
                if (!pending.GroupedWindowGraceLogged)
                {
                    pending.GroupedWindowGraceLogged = true;
                    Logger.Log(
                        "Foreground changed within a multi-window app; briefly waiting for thumbnail selection; pendingId=" +
                        pending.Id + ", candidateWindows=" + candidateWindowCount + ".");
                }
            }

            if (!TryUpdateEvaluationState(pending))
                return true;

            double graceRemaining = (pending.GroupedWindowGraceUntilUtc - DateTime.UtcNow).TotalMilliseconds;
            if (graceRemaining <= 0)
                return false;

            ScheduleEvaluation(
                Math.Max(1, Math.Min(PendingEvaluationIntervalMs, (int)Math.Ceiling(graceRemaining))),
                pending.Id);
            return true;
        }

        private PendingClick GetPending()
        {
            PendingClick pending;
            return _pendingClicks.TryGetSnapshot(out _, out pending) ? pending : null;
        }

        private void ClearPending()
        {
            lock (_sync)
            {
                _pendingClicks.Clear();
                _latestHitTestRequest = null;
                DisableEvaluationTimerLocked();
            }
        }

        private bool TryClaimPending(int pendingId)
        {
            lock (_sync)
            {
                return TryClaimPendingLocked(pendingId);
            }
        }

        private bool TryClaimPendingLocked(int pendingId)
        {
            PendingClick claimed;
            if (!_pendingClicks.TryTake(pendingId, out claimed))
                return false;

            if (_latestHitTestRequest != null && _latestHitTestRequest.PendingId == pendingId)
                _latestHitTestRequest = null;

            DisableEvaluationTimerLocked();
            return true;
        }

        private bool TryUpdateEvaluationState(PendingClick snapshot)
        {
            return _pendingClicks.TryUpdate(
                snapshot.Id,
                pending => pending.CopyEvaluationStateFrom(snapshot));
        }

        private void DisableEvaluationTimerLocked()
        {
            try { _evaluationTimer.Change(Timeout.Infinite, Timeout.Infinite); }
            catch (ObjectDisposedException) { }
        }

        private void ScheduleEvaluation(int delayMs, int? expectedPendingId = null)
        {
            if (delayMs < 1)
                delayMs = 1;

            lock (_sync)
            {
                if (_disposed ||
                    (expectedPendingId.HasValue && !_pendingClicks.IsCurrent(expectedPendingId.Value)))
                    return;

                try { _evaluationTimer.Change(delayMs, Timeout.Infinite); }
                catch (ObjectDisposedException) { }
            }
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
            public bool SawThumbnailPreview;
            public bool ThumbnailPreviewWaitLogged;
            public bool GroupedWindowGraceLogged;
            public DateTime GroupedWindowGraceUntilUtc;

            public PendingClick Clone()
            {
                return new PendingClick
                {
                    Id = Id,
                    TargetMonitor = TargetMonitor,
                    BeforeWindow = BeforeWindow,
                    BeforeMonitor = BeforeMonitor,
                    BeforeWasMaximized = BeforeWasMaximized,
                    StartedUtc = StartedUtc,
                    HitKind = HitKind,
                    SawThumbnailPreview = SawThumbnailPreview,
                    ThumbnailPreviewWaitLogged = ThumbnailPreviewWaitLogged,
                    GroupedWindowGraceLogged = GroupedWindowGraceLogged,
                    GroupedWindowGraceUntilUtc = GroupedWindowGraceUntilUtc
                };
            }

            public void CopyEvaluationStateFrom(PendingClick snapshot)
            {
                SawThumbnailPreview = snapshot.SawThumbnailPreview;
                ThumbnailPreviewWaitLogged = snapshot.ThumbnailPreviewWaitLogged;
                GroupedWindowGraceLogged = snapshot.GroupedWindowGraceLogged;
                GroupedWindowGraceUntilUtc = snapshot.GroupedWindowGraceUntilUtc;
            }
        }
    }
}
