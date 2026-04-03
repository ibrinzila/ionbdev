# Port Whisperer (.NET)

A cross-platform C# port of [port-whisperer](https://github.com/LarsenCundric/port-whisperer) — a beautiful tool to see what's running on your ports with framework detection, process info, and interactive management.

## Projects

| Project | Description |
|---------|-------------|
| **PortWhisperer.Core** | Shared library — port scanning, framework detection, process classification |
| **PortWhisperer.Cli** | CLI tool with color-coded tables (via Spectre.Console) |
| **PortWhisperer.Desktop** | Desktop app with system tray / macOS menu bar (via Avalonia) |

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## Build & Run

### CLI

```bash
# Run directly
dotnet run --project src/PortWhisperer.Cli

# Install as global tool
dotnet pack src/PortWhisperer.Cli -o ./artifacts
dotnet tool install --global --add-source ./artifacts ports

# Then use it
ports              # Show dev server ports
ports --all        # Show all listening ports
ports ps           # Show running dev processes
ports 3000         # Inspect a specific port
ports clean        # Kill orphaned/zombie processes
ports watch        # Monitor port changes in real-time
```

### Desktop App

```bash
# Run
dotnet run --project src/PortWhisperer.Desktop

# Publish for macOS
dotnet publish src/PortWhisperer.Desktop -c Release -r osx-arm64 --self-contained
dotnet publish src/PortWhisperer.Desktop -c Release -r osx-x64 --self-contained

# Publish for Linux
dotnet publish src/PortWhisperer.Desktop -c Release -r linux-x64 --self-contained

# Publish for Windows
dotnet publish src/PortWhisperer.Desktop -c Release -r win-x64 --self-contained
```

## Features

- **Port scanning** — lists all TCP listening ports with process details
- **Framework detection** — identifies Next.js, Vite, Express, Django, Rails, Rust, Go, .NET, and 30+ more
- **Docker support** — maps containers to host ports with image-based framework detection
- **Process management** — kill processes interactively, clean orphaned/zombie servers
- **Real-time monitoring** — watch mode tracks port open/close events
- **System tray / Menu bar** — desktop app lives in your macOS menu bar or system tray for quick access
- **Cross-platform** — works on macOS, Linux, and Windows

## Architecture

```
PortWhisperer.Core/
├── Models/           # PortInfo, ProcessInfo, WatchEvent
└── Services/
    ├── PortScanner.cs        # Main scanning engine (lsof/netstat)
    ├── FrameworkDetector.cs   # package.json, config files, command analysis
    ├── ProcessClassifier.cs   # Dev vs system process classification
    └── ShellRunner.cs         # Cross-platform shell command execution

PortWhisperer.Cli/
├── Program.cs         # CLI entry point & command routing
└── ConsoleDisplay.cs  # Spectre.Console table rendering

PortWhisperer.Desktop/
├── App.axaml(.cs)     # Avalonia app with tray icon setup
├── Views/             # MainWindow with DataGrid
└── ViewModels/        # MVVM with ReactiveUI
```

## Platform Notes

- **macOS/Linux**: Uses `lsof` for port scanning and `ps` for process info
- **Windows**: Uses `netstat` for port scanning and `System.Diagnostics.Process` API
- **Docker**: Detected via `docker ps` on all platforms
