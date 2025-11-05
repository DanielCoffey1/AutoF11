using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using AutoF11.Core;
using Microsoft.Extensions.Logging;
using Application = System.Windows.Application;

namespace AutoF11.UI;

/// <summary>
/// Manages the system tray icon and context menu.
/// </summary>
public class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly Logger _logger;
    private readonly Settings _settings;
    private readonly RuleEngine _ruleEngine;
    private readonly ForegroundHook _foregroundHook;
    private readonly ProcessStartWatcher _processWatcher;
    private readonly InputSender _inputSender;
    private DispatcherTimer? _pauseTimer;
    private DateTime? _pauseUntil;
    private bool _isPaused;

    public TrayIcon(
        Logger logger,
        Settings settings,
        RuleEngine ruleEngine,
        ForegroundHook foregroundHook,
        ProcessStartWatcher processWatcher,
        InputSender inputSender)
    {
        _logger = logger;
        _settings = settings;
        _ruleEngine = ruleEngine;
        _foregroundHook = foregroundHook;
        _processWatcher = processWatcher;
        _inputSender = inputSender;

        // Create tray icon
        _notifyIcon = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "AutoF11 - Automatic Fullscreen",
            Visible = true
        };

        _notifyIcon.ContextMenuStrip = CreateContextMenu();
        _notifyIcon.DoubleClick += OnTrayIconDoubleClick;

        UpdateIcon();
    }

    private Icon CreateIcon()
    {
        // Create a simple icon using a bitmap
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);
        g.FillEllipse(new SolidBrush(Color.Blue), 2, 2, 12, 12);
        g.DrawString("F", new Font("Arial", 8, System.Drawing.FontStyle.Bold), new SolidBrush(Color.White), 4, 2);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Toggle On/Off
        var toggleItem = new ToolStripMenuItem($"Auto F11: {(_settings.Enabled ? "On" : "Off")}");
        toggleItem.Click += OnToggleEnabled;
        menu.Items.Add(toggleItem);

        menu.Items.Add(new ToolStripSeparator()); // Separator

        // Per-App Rules
        var rulesItem = new ToolStripMenuItem("Per-App Rules...");
        rulesItem.Click += OnOpenRulesWindow;
        menu.Items.Add(rulesItem);

        menu.Items.Add(new ToolStripSeparator()); // Separator

        // Pause options
        var pause1Item = new ToolStripMenuItem("Pause for 1 minute");
        pause1Item.Click += (s, e) => OnPause(TimeSpan.FromMinutes(1));
        menu.Items.Add(pause1Item);

        var pause5Item = new ToolStripMenuItem("Pause for 5 minutes");
        pause5Item.Click += (s, e) => OnPause(TimeSpan.FromMinutes(5));
        menu.Items.Add(pause5Item);

        var pause15Item = new ToolStripMenuItem("Pause for 15 minutes");
        pause15Item.Click += (s, e) => OnPause(TimeSpan.FromMinutes(15));
        menu.Items.Add(pause15Item);

        menu.Items.Add(new ToolStripSeparator()); // Separator

        // Start with Windows
        var startupItem = new ToolStripMenuItem($"Start with Windows: {(StartupManager.IsStartWithWindows() ? "Yes" : "No")}");
        startupItem.Click += OnToggleStartup;
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator()); // Separator

        // Logs
        var logsItem = new ToolStripMenuItem("Logs...");
        logsItem.Click += OnOpenLogs;
        menu.Items.Add(logsItem);

        menu.Items.Add(new ToolStripSeparator()); // Separator

        // Quit
        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += OnQuit;
        menu.Items.Add(quitItem);

        return menu;
    }

    private void OnToggleEnabled(object? sender, EventArgs e)
    {
        _settings.Enabled = !_settings.Enabled;
        _settings.Save();
        UpdateIcon();
        UpdateContextMenu();

        if (_settings.Enabled)
        {
            _foregroundHook.Start();
            _processWatcher.Start();
            _foregroundHook.ClearFullscreenTracking(); // Clear tracking when enabled
            _logger.Log(LogLevel.Information, "AutoF11 enabled");
        }
        else
        {
            _foregroundHook.Stop();
            _processWatcher.Stop();
            _foregroundHook.ClearFullscreenTracking(); // Clear tracking when disabled
            _logger.Log(LogLevel.Information, "AutoF11 disabled");
        }
    }

    private void OnOpenRulesWindow(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var rulesWindow = new RulesWindow(_settings, _ruleEngine);
            rulesWindow.Show();
        });
    }

    private void OnPause(TimeSpan duration)
    {
        _isPaused = true;
        _pauseUntil = DateTime.Now.Add(duration);
        _foregroundHook.Stop();
        _processWatcher.Stop();

        // Start timer to update tooltip and resume
        _pauseTimer?.Stop();
        _pauseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _pauseTimer.Tick += OnPauseTimerTick;
        _pauseTimer.Start();

        UpdateIcon();
        UpdateContextMenu();
        _logger.Log(LogLevel.Information, $"Paused for {duration.TotalMinutes} minutes");
    }

    private void OnPauseTimerTick(object? sender, EventArgs e)
    {
        if (_pauseUntil == null)
        {
            _pauseTimer?.Stop();
            return;
        }

        var remaining = _pauseUntil.Value - DateTime.Now;
        if (remaining <= TimeSpan.Zero)
        {
            // Resume
            _isPaused = false;
            _pauseUntil = null;
            _pauseTimer?.Stop();
            _pauseTimer = null;

            if (_settings.Enabled)
            {
                _foregroundHook.Start();
                _processWatcher.Start();
                _foregroundHook.ClearFullscreenTracking(); // Clear tracking on resume
            }

            UpdateIcon();
            UpdateContextMenu();
            _logger.Log(LogLevel.Information, "Resumed from pause");
        }
        else
        {
            // Update tooltip
            var minutes = (int)remaining.TotalMinutes;
            var seconds = remaining.Seconds;
            _notifyIcon.Text = $"AutoF11 - Paused ({minutes}:{seconds:D2} remaining)";
        }
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        try
        {
            var isEnabled = StartupManager.IsStartWithWindows();
            StartupManager.SetStartWithWindows(!isEnabled);
            UpdateContextMenu();
            _logger.Log(LogLevel.Information, $"Start with Windows: {!isEnabled}");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to toggle startup: {ex.Message}");
            System.Windows.MessageBox.Show($"Failed to update startup setting: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnOpenLogs(object? sender, EventArgs e)
    {
        try
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AutoF11",
                "logs"
            );
            if (Directory.Exists(logDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", logDir);
            }
            else
            {
                System.Windows.MessageBox.Show("Log directory not found.", "Logs", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to open logs: {ex.Message}");
        }
    }

    private void OnQuit(object? sender, EventArgs e)
    {
        _logger.Log(LogLevel.Information, "Quitting AutoF11");
        Application.Current.Shutdown();
    }

    private void OnTrayIconDoubleClick(object? sender, EventArgs e)
    {
        OnOpenRulesWindow(sender, e);
    }

    private void UpdateIcon()
    {
        // Update icon based on state
        if (_isPaused)
        {
            _notifyIcon.Text = $"AutoF11 - Paused";
        }
        else if (_settings.Enabled)
        {
            _notifyIcon.Text = "AutoF11 - Enabled";
        }
        else
        {
            _notifyIcon.Text = "AutoF11 - Disabled";
        }
    }

    private void UpdateContextMenu()
    {
        _notifyIcon.ContextMenuStrip = CreateContextMenu();
    }

    public void Dispose()
    {
        _pauseTimer?.Stop();
        _notifyIcon?.Dispose();
    }
}
