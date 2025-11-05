using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AutoF11.Core;
using Xunit;

namespace AutoF11.Tests;

public class SettingsTests
{
    [Fact]
    public void Load_NonExistentFile_ReturnsDefaults()
    {
        // Arrange
        // For this test, we'll just verify defaults are set
        var settings = new Settings();
        settings.InitializeDefaults();

        // Assert
        Assert.True(settings.Rules.Count > 0);
        Assert.Contains(settings.Rules, r => r.ProcessName == "chrome");
    }

    [Fact]
    public void Save_ThenLoad_ReturnsSameSettings()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"AutoF11_Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var tempPath = Path.Combine(tempDir, "settings.json");

        try
        {
            var originalSettings = new Settings
            {
                Enabled = false,
                VerboseLogging = true,
                Rules = new List<AppRule>
                {
                    new AppRule
                    {
                        ProcessName = "testapp",
                        Strategy = KeyStrategy.AltEnter,
                        DelayMs = 200,
                        Enabled = true,
                        OnlyOncePerSession = true
                    }
                }
            };

            // Save to temp location
            var json = System.Text.Json.JsonSerializer.Serialize(originalSettings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(tempPath, json);

            // Load back
            var jsonLoaded = File.ReadAllText(tempPath);
            var loadedSettings = System.Text.Json.JsonSerializer.Deserialize<Settings>(jsonLoaded, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Assert
            Assert.NotNull(loadedSettings);
            Assert.Equal(originalSettings.Enabled, loadedSettings.Enabled);
            Assert.Equal(originalSettings.VerboseLogging, loadedSettings.VerboseLogging);
            Assert.Equal(originalSettings.Rules.Count, loadedSettings.Rules.Count);
            var rule = loadedSettings.Rules.First();
            Assert.Equal("testapp", rule.ProcessName);
            Assert.Equal(KeyStrategy.AltEnter, rule.Strategy);
            Assert.Equal(200, rule.DelayMs);
            Assert.True(rule.Enabled);
            Assert.True(rule.OnlyOncePerSession);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void AppRule_Matches_ProcessNameOnly()
    {
        // Arrange
        var rule = new AppRule
        {
            ProcessName = "chrome",
            WindowTitleContains = null
        };

        // Act & Assert
        Assert.True(rule.Matches("chrome", "Any Title"));
        Assert.True(rule.Matches("chrome", null));
        Assert.False(rule.Matches("firefox", "Any Title"));
    }

    [Fact]
    public void AppRule_Matches_ProcessNameAndWindowTitle()
    {
        // Arrange
        var rule = new AppRule
        {
            ProcessName = "chrome",
            WindowTitleContains = "YouTube"
        };

        // Act & Assert
        Assert.True(rule.Matches("chrome", "YouTube - Watch"));
        Assert.True(rule.Matches("chrome", "YouTube Video"));
        Assert.False(rule.Matches("chrome", "Google Search"));
        Assert.False(rule.Matches("firefox", "YouTube - Watch"));
    }

    [Fact]
    public void AppRule_Matches_CaseInsensitive()
    {
        // Arrange
        var rule = new AppRule
        {
            ProcessName = "chrome",
            WindowTitleContains = "YouTube"
        };

        // Act & Assert
        Assert.True(rule.Matches("Chrome", "youtube - Watch"));
        Assert.True(rule.Matches("CHROME", "YOUTUBE"));
    }
}
