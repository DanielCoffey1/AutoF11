namespace AutoF11.Core;

/// <summary>
/// Helper class for KeyStrategy enum values in XAML.
/// </summary>
public static class KeyStrategyValues
{
    public static KeyStrategy[] Values { get; } = 
    {
        KeyStrategy.None,
        KeyStrategy.F11,
        KeyStrategy.AltEnter,
        KeyStrategy.WinUp
    };
}
