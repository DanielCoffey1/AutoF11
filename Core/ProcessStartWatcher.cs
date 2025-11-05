using System;
using System.Management;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AutoF11.Core;

/// <summary>
/// Watches for new process starts using WMI and applies rules when they get a main window.
/// </summary>
public class ProcessStartWatcher : IDisposable
{
    private readonly Logger _logger;
    private readonly RuleEngine _ruleEngine;
    private readonly InputSender _inputSender;
    private ManagementEventWatcher? _watcher;

    public ProcessStartWatcher(Logger logger, RuleEngine ruleEngine, InputSender inputSender)
    {
        _logger = logger;
        _ruleEngine = ruleEngine;
        _inputSender = inputSender;
    }

    public void Start()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace");
            _watcher = new ManagementEventWatcher(query);
            _watcher.EventArrived += OnProcessStart;
            _watcher.Start();

            _logger.Log(LogLevel.Information, "Process start watcher started");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to start process watcher: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (_watcher != null)
        {
            _watcher.Stop();
            _watcher.EventArrived -= OnProcessStart;
            _watcher.Dispose();
            _watcher = null;
            _logger.Log(LogLevel.Information, "Process start watcher stopped");
        }
    }

    private void OnProcessStart(object sender, EventArrivedEventArgs e)
    {
        try
        {
            var processNameWithExt = e.NewEvent["ProcessName"]?.ToString();
            var processId = Convert.ToInt32(e.NewEvent["ProcessId"]);

            if (string.IsNullOrEmpty(processNameWithExt))
                return;

            // Skip our own process
            if (processNameWithExt.Equals("AutoF11.exe", StringComparison.OrdinalIgnoreCase))
                return;

            // Strip .exe extension to match format used by GetProcessName (Process.ProcessName)
            var processName = processNameWithExt;
            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                processName = processName.Substring(0, processName.Length - 4);
            }

            _logger.Log(LogLevel.Debug, $"Process started: {processNameWithExt} -> {processName} (PID: {processId})");

            // Wait a bit for the process to get a main window, then check
            Task.Run(async () =>
            {
                await Task.Delay(500); // Give process time to create window

                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(processId);
                    if (process.HasExited)
                        return;

                    // Wait for main window handle
                    for (int i = 0; i < 10; i++)
                    {
                        process.Refresh();
                        if (process.MainWindowHandle != IntPtr.Zero)
                        {
                            await Task.Delay(200); // Additional delay for window to be ready
                            // Use Process.ProcessName to ensure consistent format (without .exe)
                            var actualProcessName = process.ProcessName;
                            var windowTitle = Win32Interop.GetWindowTitle(process.MainWindowHandle);
                            var rule = _ruleEngine.ResolveRule(actualProcessName, windowTitle);
                            
                            if (rule != null)
                            {
                                _logger.Log(LogLevel.Information, $"Applying rule to new process {actualProcessName}: {rule.Strategy}");
                                await _inputSender.SendKeysAsync(rule.Strategy, rule.DelayMs, process.MainWindowHandle);
                            }
                            else
                            {
                                _logger.Log(LogLevel.Debug, $"No rule for new process {actualProcessName} (title: {windowTitle})");
                            }
                            return;
                        }
                        await Task.Delay(100);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Debug, $"Error checking process window: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Error in process start handler: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
