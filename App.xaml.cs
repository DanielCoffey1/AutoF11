using System;
using System.Threading;
using System.Windows;
using AutoF11.Core;
using AutoF11.UI;
using Microsoft.Extensions.Logging;

namespace AutoF11;

/// <summary>
/// Main application entry point.
/// </summary>
public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;
    private Logger? _logger;
    private Settings? _settings;
    private RuleEngine? _ruleEngine;
    private InputSender? _inputSender;
    private ForegroundHook? _foregroundHook;
    private ProcessStartWatcher? _processWatcher;
    private TrayIcon? _trayIcon;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure single instance
        bool createdNew;
        _mutex = new Mutex(true, "AutoF11_SingleInstance", out createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show("AutoF11 is already running.", "AutoF11", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            // Initialize components
            _logger = new Logger();
            _settings = Settings.Load();
            _logger.SetVerbose(_settings.VerboseLogging);

            _ruleEngine = new RuleEngine(_settings, _logger);
            _inputSender = new InputSender(_logger);
            _foregroundHook = new ForegroundHook(_logger, _ruleEngine, _inputSender);
            _processWatcher = new ProcessStartWatcher(_logger, _ruleEngine, _inputSender);

            // Start hooks if enabled
            if (_settings.Enabled)
            {
                _foregroundHook.Start();
                _processWatcher.Start();
            }

            // Create tray icon
            _trayIcon = new TrayIcon(
                _logger,
                _settings,
                _ruleEngine,
                _foregroundHook,
                _processWatcher,
                _inputSender
            );

            // Hide main window (we only use tray)
            MainWindow?.Hide();

            _logger.Log(LogLevel.Information, "AutoF11 started successfully");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to start AutoF11: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _foregroundHook?.Dispose();
            _processWatcher?.Dispose();
            _trayIcon?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();

            _logger?.Log(LogLevel.Information, "AutoF11 shutting down");
        }
        catch { }

        base.OnExit(e);
    }
}
