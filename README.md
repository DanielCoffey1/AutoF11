# AutoF11

Windows utility that hooks into foreground window events and sends F11 (or Alt+Enter) automatically. Fullscreen made effortless.

## Overview

AutoF11 is a lightweight Windows tray application that automatically toggles fullscreen mode when applications become the foreground window or when new applications launch. It provides per-app configuration, supports multiple fullscreen strategies (F11, Alt+Enter, Win+Up), and includes robust handling for edge cases.

## Features

- **Automatic Fullscreen Toggling**: Detects when windows become foreground or new processes start, and automatically sends fullscreen keys
- **Per-App Rules**: Configure different strategies (F11, Alt+Enter, Win+Up, TryF11ThenAltEnter) for different applications
- **Smart Default Behavior**: Unknown apps automatically try F11 first, then fall back to Alt+Enter if needed
- **Multiple Strategies**: Support for F11, Alt+Enter, Win+Up, and TryF11ThenAltEnter (tries F11 first, then Alt+Enter if F11 doesn't work)
- **Reliable Key Injection**: Uses SendInput with PostMessage fallback for maximum compatibility
- **Smart Cooldown**: Prevents repeated triggers for the same window within a configurable time period
- **Session Tracking**: Option to apply fullscreen only once per session per app
- **Whitelist/Blacklist**: Global whitelist and blacklist for process filtering
- **Pause Functionality**: Temporarily pause auto-fullscreen for 1, 5, or 15 minutes
- **Start with Windows**: Option to automatically start with Windows
- **Comprehensive Logging**: Detailed logging with rolling file logs and diagnostic information
- **System Tray Integration**: Clean system tray interface with context menu

## Requirements

- Windows 10/11 (x64)
- .NET 8.0 Runtime (included in self-contained build)
- Administrator privileges may be required for some elevated target windows

## Installation

### Building from Source

1. Clone the repository:
   ```bash
   git clone https://github.com/yourusername/AutoF11.git
   cd AutoF11
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the project:
   ```bash
   dotnet build
   ```

4. Publish as self-contained executable:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

The executable will be in `bin/Release/net8.0-windows/win-x64/publish/AutoF11.exe`

### Running Tests

```bash
dotnet test
```

## Usage

### Starting the Application

1. Run `AutoF11.exe`
2. The application will appear in the system tray
3. Right-click the tray icon to access the menu

### Configuring Per-App Rules

1. Right-click the tray icon and select "Per-App Rules..."
2. In the rules window, you can:
   - Add new rules by filling in a new row
   - Edit existing rules
   - Delete rules by selecting a row and pressing Delete
   - Configure:
     - **Process Name**: The process name (e.g., `chrome`, `msedge`, `firefox`)
     - **Window Title Contains**: Optional filter based on window title
     - **Strategy**: F11, Alt+Enter, Win+Up, TryF11ThenAltEnter, or None
     - **Delay (ms)**: Delay before sending keys (default: 150ms)
     - **Enabled**: Whether the rule is active
     - **Only Once**: Apply fullscreen only once per session for this app

3. Click "Apply" to save changes (applies immediately without restart)

### Default Rules

On first run, AutoF11 includes default rules for:

- **Browsers** (F11, enabled): Chrome, Edge, Firefox, Opera
- **VS Code** (F11, disabled): Disabled by default
- **Explorer** (None, disabled): Disabled by default
- **Game Launchers** (Alt+Enter): Steam, Epic Games Launcher

**Unknown Apps**: Apps without explicit rules automatically use `TryF11ThenAltEnter` strategy, which attempts F11 first and falls back to Alt+Enter if F11 doesn't work. This provides automatic support for most applications without configuration.

### Tray Menu Options

- **Auto F11: On/Off**: Toggle the main functionality
- **Per-App Rules...**: Open the rules configuration window
- **Pause for 1/5/15 minutes**: Temporarily pause auto-fullscreen
- **Start with Windows**: Toggle automatic startup with Windows
- **Logs...**: Open the logs directory in File Explorer
- **Quit**: Exit the application

## How It Works

### Foreground Window Detection

AutoF11 uses `SetWinEventHook` to monitor `EVENT_SYSTEM_FOREGROUND` events, detecting when the active window changes. When a foreground change is detected:

1. The process name and window title are extracted
2. The rule engine determines if a rule applies
3. If a rule matches and is enabled, the configured key strategy is sent after the configured delay

### Process Start Detection

AutoF11 uses WMI (`ManagementEventWatcher`) to detect when new processes start. When a new process is detected:

1. The application waits for the process to create a main window
2. The same rule matching logic applies
3. Fullscreen keys are sent if a matching rule is found

### Key Injection

AutoF11 uses `SendInput` to send keyboard input to the foreground window. If the window isn't in the foreground or `SendInput` fails, it automatically falls back to `PostMessage` for better compatibility. For elevated target windows, the application may prompt for elevation if needed.

### Strategies

- **F11**: Standard fullscreen toggle key (works for most browsers and applications)
- **Alt+Enter**: Common alternative for games and media players
- **Win+Up**: Windows key combination for maximizing windows
- **TryF11ThenAltEnter**: Tries F11 first, then automatically falls back to Alt+Enter if F11 doesn't work (default for unknown apps)
- **None**: Disable auto-fullscreen for specific apps

The `TryF11ThenAltEnter` strategy intelligently detects if F11 worked by checking if the window became borderless (fullscreen indicator). If F11 worked, it skips Alt+Enter to avoid toggling back out of fullscreen.

## Configuration

### Settings File

Settings are stored in JSON format at:
```
%APPDATA%\AutoF11\settings.json
```

### Logs

Logs are stored at:
```
%LOCALAPPDATA%\AutoF11\logs\
```

Logs are automatically rotated when they reach 5MB, keeping up to 5 log files.

## Limitations

1. **Elevated Windows**: If a target window is running with administrator privileges and AutoF11 is not, you may need to run AutoF11 as administrator or the key injection may fail
2. **UWP Apps**: Some Universal Windows Platform apps may not respond to key injection due to sandboxing
3. **Fast Window Switching**: Rapid alt-tabbing may trigger cooldown mechanisms
4. **Borderless Windows**: Some applications (especially games) may already appear fullscreen; AutoF11 will still attempt to send keys unless blacklisted

## Privacy

- **No Network Usage**: AutoF11 does not connect to the internet or send any data externally
- **Local Only**: All settings and logs are stored locally on your machine
- **No Telemetry**: No usage tracking or telemetry is collected

## Troubleshooting

### Keys Not Being Sent

1. Check if the app is in the blacklist or not in the whitelist
2. Verify the rule is enabled
3. Check if AutoF11 is paused
4. Check the logs for error messages
5. Try running AutoF11 as administrator if the target window is elevated
6. Check logs for "Window not foreground, using PostMessage" - this indicates the app is using fallback method

### Application Not Starting

1. Check if another instance is already running (AutoF11 is single-instance)
2. Check Windows Event Viewer for errors
3. Verify .NET 8.0 runtime is installed (for non-self-contained builds)

### Rules Not Applying

1. Verify the process name matches exactly (case-insensitive)
2. Check if the window title filter is correct
3. Ensure the rule is enabled
4. Check if "Only Once" is enabled and the session hasn't been cleared
5. Check logs for "Checking rules for process" to see what process name is detected
6. Unknown apps automatically use `TryF11ThenAltEnter` - check logs for "using TryF11ThenAltEnter (default heuristic)"

### Fullscreen Not Working for Specific App

1. Check the logs to see which strategy is being used
2. If the app uses `TryF11ThenAltEnter`, check logs for "F11 appears to have worked" or "F11 didn't appear to work, trying Alt+Enter"
3. If neither F11 nor Alt+Enter work, the app may not support keyboard fullscreen toggling
4. Try adding an explicit rule with a different strategy (e.g., Win+Up) or set to None to disable

## Elevation Note

AutoF11 requests elevation only when necessary. If you encounter issues with elevated target windows (e.g., some games or applications running as administrator), you may need to:

1. Run AutoF11 as administrator, or
2. The application will prompt you to restart with elevation if it detects an elevated target window

## Development

### Project Structure

```
AutoF11/
├── Core/                  # Core business logic
│   ├── Win32Interop.cs   # Win32 API interop definitions
│   ├── ForegroundHook.cs # Foreground window event hook
│   ├── ProcessStartWatcher.cs # WMI process start watcher
│   ├── InputSender.cs    # Keyboard input injection
│   ├── RuleEngine.cs     # Rule matching engine
│   ├── Settings.cs        # Settings persistence
│   ├── Logger.cs         # Logging implementation
│   ├── Elevation.cs      # Elevation detection
│   └── StartupManager.cs # Windows startup management
├── UI/                    # User interface
│   ├── TrayIcon.cs       # System tray icon and menu
│   └── RulesWindow.xaml   # Per-app rules editor
├── Tests/                 # Unit tests
│   ├── RuleEngineTests.cs
│   └── SettingsTests.cs
├── App.xaml               # Application definition
└── MainWindow.xaml       # Main window (hidden)
```

### Key Components

- **ForegroundHook**: Monitors foreground window changes using `SetWinEventHook`
- **ProcessStartWatcher**: Watches for new process starts using WMI
- **InputSender**: Sends keyboard input using `SendInput` API with `PostMessage` fallback
- **RuleEngine**: Resolves which rule (if any) applies to a process/window, with smart defaults for unknown apps
- **Settings**: Loads/saves configuration from JSON file
- **TrayIcon**: Manages system tray icon and context menu
- **Logger**: Rolling file logger with diagnostic information

## License

This project is provided as-is for personal use.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## Acknowledgments

- Uses Win32 APIs for window and input management
- Built with .NET 8 and WPF
- Inspired by the need for automatic fullscreen toggling