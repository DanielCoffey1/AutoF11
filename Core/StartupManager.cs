using Microsoft.Win32;
using System;
using System.IO;

namespace AutoF11.Core;

/// <summary>
/// Manages the "Start with Windows" registry setting.
/// </summary>
public class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunKeyName = "AutoF11";

    /// <summary>
    /// Checks if AutoF11 is set to start with Windows.
    /// </summary>
    public static bool IsStartWithWindows()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var value = key?.GetValue(RunKeyName);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sets or removes the "Start with Windows" registry entry.
    /// </summary>
    public static void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null)
                return;

            if (enable)
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                // If running from development, use the actual exe path
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                {
                    exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                }

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    key.SetValue(RunKeyName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(RunKeyName, false);
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to update startup registry: {ex.Message}", ex);
        }
    }
}
