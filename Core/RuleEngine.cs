using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace AutoF11.Core;

/// <summary>
/// Engine for resolving which key strategy to apply for a given window.
/// </summary>
public class RuleEngine
{
    private readonly Settings _settings;
    private readonly Dictionary<string, bool> _sessionApplied = new(StringComparer.OrdinalIgnoreCase);
    private readonly Logger? _logger;

    public RuleEngine(Settings settings, Logger? logger = null)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Resolves the appropriate rule for a given process and window.
    /// </summary>
    public AppRule? ResolveRule(string? processName, string? windowTitle)
    {
        if (string.IsNullOrEmpty(processName))
            return null;

        // Check global blacklist
        if (_settings.GlobalBlacklist.Any(b => processName.Equals(b, StringComparison.OrdinalIgnoreCase)))
            return null;

        // Check global whitelist (if not empty, process must be in it)
        if (_settings.GlobalWhitelist.Count > 0)
        {
            if (!_settings.GlobalWhitelist.Any(w => processName.Equals(w, StringComparison.OrdinalIgnoreCase)))
                return null;
        }

        // Find matching rule
        _logger?.Log(LogLevel.Information, $"Checking rules for process: {processName}, title: {windowTitle}");
        _logger?.Log(LogLevel.Information, $"Available rules: {_settings.Rules.Count(r => r.Enabled)} enabled, {_settings.Rules.Count} total");
        
        var rule = _settings.Rules.FirstOrDefault(r => r.Enabled && r.Matches(processName, windowTitle));
        if (rule != null)
        {
            _logger?.Log(LogLevel.Debug, $"Found matching rule: {rule.ProcessName} -> {rule.Strategy}");
            // Check if we should skip due to onlyOncePerSession
            if (rule.OnlyOncePerSession)
            {
                var key = $"{processName}_{windowTitle ?? ""}";
                if (_sessionApplied.ContainsKey(key))
                {
                    _logger?.Log(LogLevel.Debug, $"Rule already applied this session, skipping");
                    return null;
                }
                _sessionApplied[key] = true;
            }
            return rule;
        }
        
        // Log all enabled rules for debugging
        var enabledRules = _settings.Rules.Where(r => r.Enabled).ToList();
        if (enabledRules.Any())
        {
            _logger?.Log(LogLevel.Information, $"Enabled rules: {string.Join(", ", enabledRules.Select(r => r.ProcessName))}");
        }
        else
        {
            _logger?.Log(LogLevel.Warning, "No enabled rules found! Check your settings.json");
        }

        // If no rule found and prefer Alt+Enter for games, check if it might be a game
        // Default to TryF11ThenAltEnter for unknown apps - tries F11 first, then Alt+Enter as fallback
        if (_settings.PreferAltEnterForUnknownGames && MightBeGame(processName))
        {
            _logger?.Log(LogLevel.Information, $"No rule for {processName}, using TryF11ThenAltEnter (default heuristic)");
            return new AppRule
            {
                ProcessName = processName,
                Strategy = KeyStrategy.TryF11ThenAltEnter,
                DelayMs = 150,
                Enabled = true,
                OnlyOncePerSession = false
            };
        }

        _logger?.Log(LogLevel.Debug, $"No rule found for {processName} and not a game");
        return null;
    }

    /// <summary>
    /// Clears the session tracking (called when app is paused/resumed or on startup).
    /// </summary>
    public void ClearSession()
    {
        _sessionApplied.Clear();
    }

    private bool MightBeGame(string processName)
    {
        // Heuristic: games often have executable names that are not common apps
        var commonApps = new[] { "explorer", "chrome", "firefox", "msedge", "code", "notepad", "winword", "excel", "powerpnt" };
        return !commonApps.Contains(processName, StringComparer.OrdinalIgnoreCase);
    }
}
