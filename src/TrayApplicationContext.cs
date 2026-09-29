// TaskbarFetch
// Copyright (c) 2026 Thyann Seng
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TaskbarFetch
{
    internal sealed class TrayApplicationContext : ApplicationContext, IDisposable
    {
        private readonly NotifyIcon _notifyIcon;
        private readonly Icon _icon;
        private readonly ToolStripMenuItem _pauseItem;
        private readonly ToolStripMenuItem _startupItem;
        private readonly ToolStripMenuItem _moveAlreadyActiveItem;
        private readonly TaskbarFetchEngine _engine;
        private bool _disposed;

        public TrayApplicationContext()
        {
            _engine = new TaskbarFetchEngine();
            _engine.MoveAlreadyActiveWindowOnCrossMonitorClick = UserSettingsManager.GetMoveAlreadyActiveWindowOnCrossMonitorClick();

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

            _moveAlreadyActiveItem = new ToolStripMenuItem("Move already-active window to clicked monitor");
            _moveAlreadyActiveItem.Checked = _engine.MoveAlreadyActiveWindowOnCrossMonitorClick;
            _moveAlreadyActiveItem.CheckOnClick = false;
            _moveAlreadyActiveItem.ToolTipText = "When enabled, clicking this window's taskbar button on another monitor moves it there even if it stays active.";
            _moveAlreadyActiveItem.Click += delegate { ToggleMoveAlreadyActiveWindow(); };
            menu.Items.Add(_moveAlreadyActiveItem);

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

        private void ToggleMoveAlreadyActiveWindow()
        {
            try
            {
                bool enable = !_engine.MoveAlreadyActiveWindowOnCrossMonitorClick;
                UserSettingsManager.SetMoveAlreadyActiveWindowOnCrossMonitorClick(enable);
                _engine.MoveAlreadyActiveWindowOnCrossMonitorClick = enable;
                _moveAlreadyActiveItem.Checked = enable;
                Logger.Log(enable
                    ? "Already-active cross-monitor move enabled."
                    : "Already-active cross-monitor move disabled.");
            }
            catch (Exception ex)
            {
                Logger.LogException("Failed to change already-active window setting", ex);
                MessageBox.Show(
                    "Could not change the already-active window setting.\r\n\r\n" + ex.Message,
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
}
