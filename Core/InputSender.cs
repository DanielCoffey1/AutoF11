using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AutoF11.Core;

/// <summary>
/// Handles sending keyboard input to windows using SendInput.
/// </summary>
public class InputSender
{
    private readonly Logger _logger;

    public InputSender(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Sends the specified key strategy to the specified window.
    /// </summary>
    public async Task SendKeysAsync(KeyStrategy strategy, int delayMs, IntPtr hWnd = default)
    {
        if (strategy == KeyStrategy.None)
            return;

        // Wait for the delay
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }

        try
        {
            // If no window handle provided, use current foreground window
            if (hWnd == IntPtr.Zero)
            {
                hWnd = Win32Interop.GetForegroundWindow();
            }

            if (hWnd == IntPtr.Zero || !Win32Interop.IsWindow(hWnd))
            {
                _logger.Log(LogLevel.Warning, "No valid window to send keys to");
                return;
            }

            // Ensure window is foreground (attempt with AttachThreadInput if needed)
            EnsureForeground(hWnd);
            
            // Verify window is actually foreground before sending keys
            var currentForeground = Win32Interop.GetForegroundWindow();
            if (currentForeground != hWnd)
            {
                _logger.Log(LogLevel.Warning, $"Window {hWnd} is not foreground (current: {currentForeground}), attempting to focus again");
                // Try again with more aggressive focusing
                Win32Interop.SetForegroundWindow(hWnd);
                await Task.Delay(100);
                currentForeground = Win32Interop.GetForegroundWindow();
                if (currentForeground != hWnd)
                {
                    _logger.Log(LogLevel.Warning, $"Failed to bring window to foreground, using PostMessage instead");
                }
            }
            
            // Small delay to ensure window is actually foreground
            await Task.Delay(50);

            // Check if window is still foreground - if not, use PostMessage
            currentForeground = Win32Interop.GetForegroundWindow();
            bool usePostMessage = (currentForeground != hWnd);
            
            if (usePostMessage)
            {
                _logger.Log(LogLevel.Information, "Window not foreground, using PostMessage instead of SendInput");
            }
            
            switch (strategy)
            {
                case KeyStrategy.F11:
                    if (usePostMessage)
                        SendF11PostMessage(hWnd);
                    else
                        SendF11();
                    break;
                case KeyStrategy.AltEnter:
                    if (usePostMessage)
                        SendAltEnterPostMessage(hWnd);
                    else
                        SendAltEnter();
                    break;
                case KeyStrategy.WinUp:
                    if (usePostMessage)
                        SendWinUpPostMessage(hWnd);
                    else
                        SendWinUp();
                    break;
                case KeyStrategy.TryF11ThenAltEnter:
                    // Try F11 first
                    _logger.Log(LogLevel.Information, "Trying F11 first");
                    bool wasBorderlessBefore = Win32Interop.IsWindowBorderless(hWnd);
                    
                    if (usePostMessage)
                        SendF11PostMessage(hWnd);
                    else
                        SendF11();
                    
                    // Wait a bit to see if F11 worked
                    await Task.Delay(400);
                    
                    // Check if window became borderless (fullscreen indicator)
                    bool isBorderlessAfter = Win32Interop.IsWindowBorderless(hWnd);
                    bool mightHaveWorked = isBorderlessAfter && !wasBorderlessBefore;
                    
                    if (mightHaveWorked)
                    {
                        _logger.Log(LogLevel.Information, "F11 appears to have worked (window became borderless), skipping Alt+Enter");
                    }
                    else
                    {
                        // Try Alt+Enter as fallback
                        _logger.Log(LogLevel.Information, "F11 didn't appear to work, trying Alt+Enter as fallback");
                        if (usePostMessage)
                            SendAltEnterPostMessage(hWnd);
                        else
                            SendAltEnter();
                    }
                    break;
            }

            _logger.Log(LogLevel.Information, $"Sent {strategy} to window");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"Failed to send keys: {ex.Message}");
        }
    }

    private void EnsureForeground(IntPtr hWnd)
    {
        var foregroundThread = Win32Interop.GetWindowThreadProcessId(Win32Interop.GetForegroundWindow(), out _);
        var targetThread = Win32Interop.GetWindowThreadProcessId(hWnd, out _);

        if (foregroundThread != targetThread)
        {
            Win32Interop.AttachThreadInput(foregroundThread, targetThread, true);
            Win32Interop.SetForegroundWindow(hWnd);
            Win32Interop.AttachThreadInput(foregroundThread, targetThread, false);
        }
        else
        {
            Win32Interop.SetForegroundWindow(hWnd);
        }
    }

    private void SendF11()
    {
        var inputs = new[]
        {
            new Win32Interop.INPUT
            {
                type = Win32Interop.INPUT_KEYBOARD,
                ki = new Win32Interop.KEYBDINPUT
                {
                    wVk = Win32Interop.VK_F11,
                    wScan = 0,
                    dwFlags = 0,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            },
            new Win32Interop.INPUT
            {
                type = Win32Interop.INPUT_KEYBOARD,
                ki = new Win32Interop.KEYBDINPUT
                {
                    wVk = Win32Interop.VK_F11,
                    wScan = 0,
                    dwFlags = Win32Interop.KEYEVENTF_KEYUP,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        var result = Win32Interop.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32Interop.INPUT>());
        if (result != 2)
        {
            _logger.Log(LogLevel.Warning, $"SendInput for F11 failed: expected 2 inputs, got {result}");
            // Try PostMessage as fallback
            var hWnd = Win32Interop.GetForegroundWindow();
            if (hWnd != IntPtr.Zero)
            {
                Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYDOWN, new IntPtr(Win32Interop.VK_F11), IntPtr.Zero);
                System.Threading.Thread.Sleep(10);
                Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYUP, new IntPtr(Win32Interop.VK_F11), IntPtr.Zero);
                _logger.Log(LogLevel.Information, "Sent F11 via PostMessage as fallback");
            }
        }
    }

    private void SendAltEnter()
    {
        // Alt down
        var altDown = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_LMENU,
                wScan = 0,
                dwFlags = 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        // Enter down
        var enterDown = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_RETURN,
                wScan = 0,
                dwFlags = 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        // Enter up
        var enterUp = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_RETURN,
                wScan = 0,
                dwFlags = Win32Interop.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        // Alt up
        var altUp = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_LMENU,
                wScan = 0,
                dwFlags = Win32Interop.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        var inputs = new[] { altDown, enterDown, enterUp, altUp };
        var result = Win32Interop.SendInput(4, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32Interop.INPUT>());
        if (result != 4)
        {
            _logger.Log(LogLevel.Warning, $"SendInput for Alt+Enter failed: expected 4 inputs, got {result}");
        }
    }

    private void SendF11PostMessage(IntPtr hWnd)
    {
        // Construct proper lParam for keyboard messages
        // lParam format: bits 0-15: repeat count, bits 16-23: scan code, bits 24-28: extended key flags, bits 29: context, bit 30: previous key state, bit 31: transition state
        // For F11: scan code is 0x57, extended key flag is 0
        IntPtr lParamDown = new IntPtr(0x00570001); // repeat count 1, scan code 0x57
        IntPtr lParamUp = new IntPtr(0xC0570001); // repeat count 1, scan code 0x57, previous key state 1, transition state 1
        
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYDOWN, new IntPtr(Win32Interop.VK_F11), lParamDown);
        System.Threading.Thread.Sleep(20);
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYUP, new IntPtr(Win32Interop.VK_F11), lParamUp);
    }

    private void SendAltEnterPostMessage(IntPtr hWnd)
    {
        // Alt key scan code: 0x38, Enter key scan code: 0x1C
        IntPtr altDown = new IntPtr(0x00380001); // Alt down
        IntPtr enterDown = new IntPtr(0x001C0001); // Enter down
        IntPtr enterUp = new IntPtr(0xC01C0001); // Enter up
        IntPtr altUp = new IntPtr(0xC0380001); // Alt up
        
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_SYSKEYDOWN, new IntPtr(Win32Interop.VK_LMENU), altDown);
        System.Threading.Thread.Sleep(20);
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYDOWN, new IntPtr(Win32Interop.VK_RETURN), enterDown);
        System.Threading.Thread.Sleep(20);
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYUP, new IntPtr(Win32Interop.VK_RETURN), enterUp);
        System.Threading.Thread.Sleep(20);
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_SYSKEYUP, new IntPtr(Win32Interop.VK_LMENU), altUp);
    }

    private void SendWinUpPostMessage(IntPtr hWnd)
    {
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYDOWN, new IntPtr(Win32Interop.VK_LWIN), IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYDOWN, new IntPtr(Win32Interop.VK_UP), IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYUP, new IntPtr(Win32Interop.VK_UP), IntPtr.Zero);
        System.Threading.Thread.Sleep(10);
        Win32Interop.PostMessage(hWnd, Win32Interop.WM_KEYUP, new IntPtr(Win32Interop.VK_LWIN), IntPtr.Zero);
    }

    private void SendWinUp()
    {
        // Win down
        var winDown = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_LWIN,
                wScan = 0,
                dwFlags = 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        // Up down
        var upDown = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_UP,
                wScan = 0,
                dwFlags = 0,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        // Up up
        var upUp = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_UP,
                wScan = 0,
                dwFlags = Win32Interop.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        // Win up
        var winUp = new Win32Interop.INPUT
        {
            type = Win32Interop.INPUT_KEYBOARD,
            ki = new Win32Interop.KEYBDINPUT
            {
                wVk = Win32Interop.VK_LWIN,
                wScan = 0,
                dwFlags = Win32Interop.KEYEVENTF_KEYUP,
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        var inputs = new[] { winDown, upDown, upUp, winUp };
        var result = Win32Interop.SendInput(4, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32Interop.INPUT>());
        if (result != 4)
        {
            _logger.Log(LogLevel.Warning, $"SendInput for Win+Up failed: expected 4 inputs, got {result}");
        }
    }
}
