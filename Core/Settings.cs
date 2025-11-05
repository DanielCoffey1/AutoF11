using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoF11.Core;

/// <summary>
/// Application settings with per-app rules and global configuration.
/// </summary>
public class Settings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AutoF11",
        "settings.json"
    );

    public bool Enabled { get; set; } = true;
    public bool VerboseLogging { get; set; } = false;
    public List<AppRule> Rules { get; set; } = new();
    public List<string> GlobalWhitelist { get; set; } = new();
    public List<string> GlobalBlacklist { get; set; } = new();
    public bool PreferAltEnterForUnknownGames { get; set; } = true;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
                if (settings != null)
                {
                    // Ensure rules list is initialized if it was null
                    settings.Rules ??= new List<AppRule>();
                    settings.GlobalWhitelist ??= new List<string>();
                    settings.GlobalBlacklist ??= new List<string>();
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
        }

        // Return defaults
        var defaultSettings = new Settings();
        defaultSettings.InitializeDefaults();
        defaultSettings.Save(); // Save defaults so user can see them
        return defaultSettings;
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }

    public void InitializeDefaults()
    {
        // Common browsers - enabled by default with F11
        Rules.AddRange(new[]
        {
            new AppRule
            {
                ProcessName = "chrome",
                Strategy = KeyStrategy.F11,
                DelayMs = 150,
                Enabled = true,
                OnlyOncePerSession = false
            },
            new AppRule
            {
                ProcessName = "msedge",
                Strategy = KeyStrategy.F11,
                DelayMs = 150,
                Enabled = true,
                OnlyOncePerSession = false
            },
            new AppRule
            {
                ProcessName = "firefox",
                Strategy = KeyStrategy.F11,
                DelayMs = 150,
                Enabled = true,
                OnlyOncePerSession = false
            },
            new AppRule
            {
                ProcessName = "opera",
                Strategy = KeyStrategy.F11,
                DelayMs = 150,
                Enabled = true,
                OnlyOncePerSession = false
            }
        });

        // VS Code - disabled by default
        Rules.Add(new AppRule
        {
            ProcessName = "code",
            Strategy = KeyStrategy.F11,
            DelayMs = 150,
            Enabled = false,
            OnlyOncePerSession = false
        });

        // Explorer - disabled by default
        Rules.Add(new AppRule
        {
            ProcessName = "explorer",
            Strategy = KeyStrategy.None,
            DelayMs = 0,
            Enabled = false,
            OnlyOncePerSession = false
        });

        // Common game launchers - Alt+Enter
        Rules.AddRange(new[]
        {
            new AppRule
            {
                ProcessName = "steam",
                Strategy = KeyStrategy.AltEnter,
                DelayMs = 200,
                Enabled = true,
                OnlyOncePerSession = true
            },
            new AppRule
            {
                ProcessName = "epicgameslauncher",
                Strategy = KeyStrategy.AltEnter,
                DelayMs = 200,
                Enabled = true,
                OnlyOncePerSession = true
            }
        });

        // Desktop/Shell - blacklisted
        GlobalBlacklist.Add("explorer"); // Desktop windows
    }
}

/// <summary>
/// Strategy for sending fullscreen keys.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KeyStrategy
{
    None,
    F11,
    AltEnter,
    WinUp,
    TryF11ThenAltEnter
}

/// <summary>
/// Rule for a specific application.
/// </summary>
public class AppRule
{
    public string ProcessName { get; set; } = string.Empty;
    public string? WindowTitleContains { get; set; }
    public KeyStrategy Strategy { get; set; } = KeyStrategy.F11;
    public int DelayMs { get; set; } = 150;
    public bool Enabled { get; set; } = true;
    public bool OnlyOncePerSession { get; set; } = false;

    public bool Matches(string? processName, string? windowTitle)
    {
        if (string.IsNullOrEmpty(processName))
            return false;

        if (!processName.Equals(ProcessName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(WindowTitleContains))
        {
            if (string.IsNullOrEmpty(windowTitle))
                return false;
            if (!windowTitle.Contains(WindowTitleContains, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
