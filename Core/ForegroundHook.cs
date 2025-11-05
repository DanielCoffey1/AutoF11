using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AutoF11.Core;

/// <summary>
/// Monitors foreground window changes using SetWinEventHook.
/// </summary>
public class ForegroundHook : IDisposable
{
    private readonly Logger _logger;
    private readonly RuleEngine _ruleEngine;
    private readonly InputSender _inputSender;
    private IntPtr _hookHandle = IntPtr.Zero;
    private readonly object _lock = new();
    private DateTime _lastEventTime = DateTime.MinValue;
    private IntPtr _lastWindowHandle = IntPtr.Zero;
    private const int CooldownMs = 2000; // 2 second cooldown for same window
    private readonly HashSet<IntPtr> _fullscreenedWindows = new(); // Track windows that are already fullscreen

    public event EventHandler<ForegroundChangedEventArgs>? ForegroundChanged;

    public ForegroundHook(Logger logger, RuleEngine ruleEngine, InputSender inputSender)
    {
        _logger = logger;
        _ruleEngine = ruleEngine;
        _inputSender = inputSender;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (_hookHandle != IntPtr.Zero)
                return;

            _hookHandle = Win32Interop.SetWinEventHook(
                Win32Interop.EVENT_SYSTEM_FOREGROUND,
                Win32Interop.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                WinEventCallback,
                0,
                0,
                Win32Interop.WINEVENT_OUTOFCONTEXT
            );

            if (_hookHandle == IntPtr.Zero)
            {
                _logger.Log(LogLevel.Error, "Failed to set WinEvent hook");
            }
            else
            {
                _logger.Log(LogLevel.Information, "Foreground hook started");
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            if (_hookHandle != IntPtr.Zero)
            {
                Win32Interop.UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _logger.Log(LogLevel.Information, "Foreground hook stopped");
            }
        }
    }

    private void WinEventCallback(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsTimeStamp)
    {
        try
        {
            // Skip if window is invalid or not visible
            if (hwnd == IntPtr.Zero || !Win32Interop.IsWindow(hwnd) || !Win32Interop.IsWindowVisible(hwnd))
                return;

            // Skip desktop/shell windows
            var className = Win32Interop.GetWindowClassName(hwnd);
            if (className == "Progman" || className == "Shell_TrayWnd" || className == "WorkerW")
                return;

            // Cooldown check - but only if we haven't already fullscreened this window
            bool alreadyFullscreened;
            lock (_lock)
            {
                alreadyFullscreened = _fullscreenedWindows.Contains(hwnd);
            }
            
            // If already fullscreened, skip cooldown check and just check if we should skip
            var now = DateTime.Now;
            if (!alreadyFullscreened && hwnd == _lastWindowHandle)
            {
                var elapsed = (now - _lastEventTime).TotalMilliseconds;
                if (elapsed < CooldownMs)
                {
                    _logger.Log(LogLevel.Debug, $"Skipping duplicate event for same window (cooldown: {elapsed:F0}ms)");
                    return;
                }
            }

            _lastWindowHandle = hwnd;
            _lastEventTime = now;

            var processName = Win32Interop.GetProcessName(hwnd);
            var windowTitle = Win32Interop.GetWindowTitle(hwnd);

            // Skip our own windows
            if (processName?.Equals("AutoF11", StringComparison.OrdinalIgnoreCase) == true)
                return;

            _logger.Log(LogLevel.Information, $"Foreground changed: {processName} - {windowTitle}");

            ForegroundChanged?.Invoke(this, new ForegroundChangedEventArgs
            {
                WindowHandle = hwnd,
                ProcessName = processName,
                WindowTitle = windowTitle
            });

            // Process the change asynchronously
            Task.Run(async () => await ProcessForegroundChange(hwnd, processName, windowTitle));
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error in WinEvent callback: {ex.Message}");
        }
    }

    private async Task ProcessForegroundChange(IntPtr hWnd, string? processName, string? windowTitle)
    {
        try
        {
            // Check if window is still valid
            if (!Win32Interop.IsWindow(hWnd))
            {
                // Remove invalid handle from tracking
                lock (_lock)
                {
                    _fullscreenedWindows.Remove(hWnd);
                }
                return;
            }
            
            var rule = _ruleEngine.ResolveRule(processName, windowTitle);
            if (rule == null)
            {
                _logger.Log(LogLevel.Information, $"No rule for {processName} (title: {windowTitle}), skipping");
                return;
            }

            // Check if window is already fullscreen (borderless)
            bool isAlreadyFullscreen = Win32Interop.IsWindowBorderless(hWnd);
            
            // Check if we've already fullscreened this window in this session
            // Use a lock to safely check the HashSet
            bool wasFullscreened;
            lock (_lock)
            {
                wasFullscreened = _fullscreenedWindows.Contains(hWnd);
            }
            
            // Skip if already fullscreen and we've already processed it, unless rule says to always toggle
            if (isAlreadyFullscreen && wasFullscreened && !rule.AlwaysToggle)
            {
                _logger.Log(LogLevel.Information, $"Window {processName} is already fullscreen, skipping (AlwaysToggle: {rule.AlwaysToggle})");
                return;
            }
            
            // If window is borderless but we haven't tracked it, it might have been manually fullscreened
            // In this case, we should still track it but not send keys (unless AlwaysToggle is true)
            if (isAlreadyFullscreen && !wasFullscreened && !rule.AlwaysToggle)
            {
                _logger.Log(LogLevel.Information, $"Window {processName} is already fullscreen (manually?), tracking but not sending keys");
                lock (_lock)
                {
                    _fullscreenedWindows.Add(hWnd);
                }
                return;
            }

            _logger.Log(LogLevel.Information, $"Applying rule for {processName}: {rule.Strategy} (delay: {rule.DelayMs}ms)");

            await _inputSender.SendKeysAsync(rule.Strategy, rule.DelayMs, hWnd);
            
            // Track that we've fullscreened this window (only if keys were sent)
            lock (_lock)
            {
                _fullscreenedWindows.Add(hWnd);
            }
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error processing foreground change: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Clears the fullscreened windows tracking (called when app is paused/resumed or on startup).
    /// Also removes invalid window handles.
    /// </summary>
    public void ClearFullscreenTracking()
    {
        lock (_lock)
        {
            // Remove invalid window handles
            var invalidHandles = _fullscreenedWindows.Where(h => !Win32Interop.IsWindow(h)).ToList();
            foreach (var handle in invalidHandles)
            {
                _fullscreenedWindows.Remove(handle);
            }
            // Optionally clear all - for now, just remove invalid ones
            // _fullscreenedWindows.Clear();
        }
    }
    
    /// <summary>
    /// Removes a specific window handle from tracking (when window is closed).
    /// </summary>
    public void RemoveWindowTracking(IntPtr hWnd)
    {
        lock (_lock)
        {
            _fullscreenedWindows.Remove(hWnd);
        }
    }

    public void Dispose()
    {
        Stop();
    }
}

public class ForegroundChangedEventArgs : EventArgs
{
    public IntPtr WindowHandle { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
}
