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
        var rule = _settings.Rules.FirstOrDefault(r => r.Enabled && r.Matches(processName, windowTitle));
        if (rule != null)
        {
            // Check if we should skip due to onlyOncePerSession
            if (rule.OnlyOncePerSession)
            {
                var key = $"{processName}_{windowTitle ?? ""}";
                if (_sessionApplied.ContainsKey(key))
                    return null;
                _sessionApplied[key] = true;
            }
            return rule;
        }

        // If no rule found and prefer Alt+Enter for games, check if it might be a game
        if (_settings.PreferAltEnterForUnknownGames && MightBeGame(processName))
        {
            _logger?.Log(LogLevel.Information, $"No rule for {processName}, using Alt+Enter (game heuristic)");
            return new AppRule
            {
                ProcessName = processName,
                Strategy = KeyStrategy.AltEnter,
                DelayMs = 200,
                Enabled = true,
                OnlyOncePerSession = false
            };
        }

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
