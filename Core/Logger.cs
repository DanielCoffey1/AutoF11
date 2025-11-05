using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace AutoF11.Core;

/// <summary>
/// Lightweight rolling file logger for AutoF11.
/// </summary>
public class Logger : ILogger
{
    private readonly string _logDirectory;
    private readonly string _logFile;
    private readonly object _lock = new();
    private readonly int _maxFileSizeBytes = 5 * 1024 * 1024; // 5 MB
    private readonly int _maxFiles = 5;
    private bool _verbose;

    public Logger()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoF11",
            "logs"
        );
        Directory.CreateDirectory(_logDirectory);
        _logFile = Path.Combine(_logDirectory, $"AutoF11_{DateTime.Now:yyyyMMdd}.log");
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel)
    {
        if (!_verbose && logLevel > LogLevel.Information)
            return false;
        return true;
    }

    public void SetVerbose(bool verbose)
    {
        _verbose = verbose;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var level = logLevel.ToString().ToUpper().PadRight(5);
        var logEntry = $"[{timestamp}] {level} {message}";

        if (exception != null)
        {
            logEntry += $"\n{exception}";
        }

        lock (_lock)
        {
            try
            {
                // Check if we need to roll over
                if (File.Exists(_logFile))
                {
                    var fileInfo = new FileInfo(_logFile);
                    if (fileInfo.Length > _maxFileSizeBytes)
                    {
                        RollOverLogs();
                    }
                }

                File.AppendAllText(_logFile, logEntry + Environment.NewLine);
            }
            catch
            {
                // Silently fail if logging fails
            }
        }
    }

    private void RollOverLogs()
    {
        try
        {
            // Delete oldest log if we have too many
            var logFiles = Directory.GetFiles(_logDirectory, "AutoF11_*.log");
            if (logFiles.Length >= _maxFiles)
            {
                Array.Sort(logFiles);
                File.Delete(logFiles[0]);
            }

            // Rename current log
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var newName = Path.Combine(_logDirectory, $"AutoF11_{timestamp}.log");
            if (File.Exists(_logFile))
            {
                File.Move(_logFile, newName);
            }
        }
        catch
        {
            // Silently fail
        }
    }
}
