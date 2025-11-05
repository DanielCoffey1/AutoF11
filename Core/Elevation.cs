using System;
using System.Diagnostics;
using System.Security.Principal;

namespace AutoF11.Core;

/// <summary>
/// Handles elevation detection and prompting for admin rights when needed.
/// </summary>
public static class Elevation
{
    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    public static bool IsElevated()
    {
        try
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a target window's process is elevated.
    /// </summary>
    public static bool IsTargetElevated(IntPtr hWnd)
    {
        try
        {
            Win32Interop.GetWindowThreadProcessId(hWnd, out uint processId);
            if (processId == 0)
                return false;

            var process = Process.GetProcessById((int)processId);
            return IsProcessElevated(process);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if a process is elevated.
    /// </summary>
    public static bool IsProcessElevated(Process process)
    {
        try
        {
            IntPtr hToken = IntPtr.Zero;
            if (!OpenProcessToken(process.Handle, TOKEN_QUERY, out hToken))
                return false;

            try
            {
                if (!GetTokenInformation(hToken, TOKEN_INFORMATION_CLASS.TokenElevation, out TOKEN_ELEVATION elevation, sizeof(uint), out _))
                    return false;

                return elevation.TokenIsElevated != 0;
            }
            finally
            {
                if (hToken != IntPtr.Zero)
                    CloseHandle(hToken);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Restarts the current application with administrator privileges.
    /// </summary>
    public static void RestartElevated()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath))
                exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            Process.Start(startInfo);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to restart elevated: {ex.Message}", ex);
        }
    }

    // Win32 API for token elevation check
    [System.Runtime.InteropServices.DllImport("advapi32.dll")]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);

    [System.Runtime.InteropServices.DllImport("advapi32.dll")]
    private static extern bool GetTokenInformation(
        IntPtr TokenHandle,
        TOKEN_INFORMATION_CLASS TokenInformationClass,
        out TOKEN_ELEVATION TokenInformation,
        int TokenInformationLength,
        out int ReturnLength
    );

    private const uint TOKEN_QUERY = 0x0008;

    private enum TOKEN_INFORMATION_CLASS
    {
        TokenElevation = 20
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct TOKEN_ELEVATION
    {
        public uint TokenIsElevated;
    }
}
