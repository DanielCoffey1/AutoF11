using System.Collections.Generic;
using AutoF11.Core;
using Xunit;

namespace AutoF11.Tests;

public class RuleEngineTests
{
    [Fact]
    public void ResolveRule_MatchesProcessName_ReturnsRule()
    {
        // Arrange
        var settings = new Settings
        {
            Rules = new List<AppRule>
            {
                new AppRule
                {
                    ProcessName = "chrome",
                    Strategy = KeyStrategy.F11,
                    Enabled = true
                }
            }
        };
        var engine = new RuleEngine(settings);

        // Act
        var rule = engine.ResolveRule("chrome", "Some Window Title");

        // Assert
        Assert.NotNull(rule);
        Assert.Equal(KeyStrategy.F11, rule.Strategy);
    }

    [Fact]
    public void ResolveRule_DisabledRule_ReturnsNull()
    {
        // Arrange
        var settings = new Settings
        {
            Rules = new List<AppRule>
            {
                new AppRule
                {
                    ProcessName = "chrome",
                    Strategy = KeyStrategy.F11,
                    Enabled = false
                }
            }
        };
        var engine = new RuleEngine(settings);

        // Act
        var rule = engine.ResolveRule("chrome", "Some Window Title");

        // Assert
        Assert.Null(rule);
    }

    [Fact]
    public void ResolveRule_BlacklistedProcess_ReturnsNull()
    {
        // Arrange
        var settings = new Settings
        {
            Rules = new List<AppRule>
            {
                new AppRule
                {
                    ProcessName = "chrome",
                    Strategy = KeyStrategy.F11,
                    Enabled = true
                }
            },
            GlobalBlacklist = new List<string> { "chrome" }
        };
        var engine = new RuleEngine(settings);

        // Act
        var rule = engine.ResolveRule("chrome", "Some Window Title");

        // Assert
        Assert.Null(rule);
    }

    [Fact]
    public void ResolveRule_Whitelist_OnlyAllowsWhitelisted()
    {
        // Arrange
        var settings = new Settings
        {
            Rules = new List<AppRule>
            {
                new AppRule
                {
                    ProcessName = "chrome",
                    Strategy = KeyStrategy.F11,
                    Enabled = true
                },
                new AppRule
                {
                    ProcessName = "firefox",
                    Strategy = KeyStrategy.F11,
                    Enabled = true
                }
            },
            GlobalWhitelist = new List<string> { "chrome" }
        };
        var engine = new RuleEngine(settings);

        // Act
        var chromeRule = engine.ResolveRule("chrome", "Title");
        var firefoxRule = engine.ResolveRule("firefox", "Title");

        // Assert
        Assert.NotNull(chromeRule);
        Assert.Null(firefoxRule);
    }

    [Fact]
    public void ResolveRule_MatchesWindowTitle_ReturnsRule()
    {
        // Arrange
        var settings = new Settings
        {
            Rules = new List<AppRule>
            {
                new AppRule
                {
                    ProcessName = "chrome",
                    WindowTitleContains = "YouTube",
                    Strategy = KeyStrategy.F11,
                    Enabled = true
                }
            }
        };
        var engine = new RuleEngine(settings);

        // Act
        var matchingRule = engine.ResolveRule("chrome", "YouTube - Watch");
        var nonMatchingRule = engine.ResolveRule("chrome", "Google Search");

        // Assert
        Assert.NotNull(matchingRule);
        Assert.Null(nonMatchingRule);
    }

    [Fact]
    public void ResolveRule_OnlyOncePerSession_ReturnsNullOnSecondCall()
    {
        // Arrange
        var settings = new Settings
        {
            Rules = new List<AppRule>
            {
                new AppRule
                {
                    ProcessName = "chrome",
                    Strategy = KeyStrategy.F11,
                    Enabled = true,
                    OnlyOncePerSession = true
                }
            }
        };
        var engine = new RuleEngine(settings);

        // Act
        var firstRule = engine.ResolveRule("chrome", "Title");
        var secondRule = engine.ResolveRule("chrome", "Title");

        // Assert
        Assert.NotNull(firstRule);
        Assert.Null(secondRule);
    }

    [Fact]
    public void ResolveRule_OnlyOncePerSession_ReturnsRuleOnSecondCallAfterClear()
    {
        // Arrange
        var settings = new Settings
        {
            Rules = new List<AppRule>
            {
                new AppRule
                {
                    ProcessName = "chrome",
                    Strategy = KeyStrategy.F11,
                    Enabled = true,
                    OnlyOncePerSession = true
                }
            }
        };
        var engine = new RuleEngine(settings);

        // Act
        var firstRule = engine.ResolveRule("chrome", "Title");
        engine.ClearSession();
        var secondRule = engine.ResolveRule("chrome", "Title");

        // Assert
        Assert.NotNull(firstRule);
        Assert.NotNull(secondRule);
    }

    [Fact]
    public void ResolveRule_UnknownGame_ReturnsAltEnterWhenPreferenceEnabled()
    {
        // Arrange
        var settings = new Settings
        {
            PreferAltEnterForUnknownGames = true
        };
        var engine = new RuleEngine(settings);

        // Act
        var rule = engine.ResolveRule("SomeGame", "Game Window");

        // Assert
        Assert.NotNull(rule);
        Assert.Equal(KeyStrategy.AltEnter, rule.Strategy);
    }

    [Fact]
    public void ResolveRule_KnownApp_DoesNotReturnAltEnterForUnknown()
    {
        // Arrange
        var settings = new Settings
        {
            PreferAltEnterForUnknownGames = true
        };
        var engine = new RuleEngine(settings);

        // Act
        var rule = engine.ResolveRule("chrome", "Browser Window");

        // Assert
        Assert.Null(rule); // Chrome is a known app, not a game
    }
}
