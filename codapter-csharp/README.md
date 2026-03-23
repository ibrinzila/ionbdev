# Codapter C# MVC ASP.NET

A full C# ASP.NET MVC port of [kcosr/codapter](https://github.com/kcosr/codapter) — a protocol adapter that enables Codex app-server clients to work with alternative AI backends.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Codex Client (Desktop / CLI / Third-party)                 │
│  ─────────────────────────────────────────                  │
│  Connects via HTTP, WebSocket, or SignalR                   │
└──────────────┬──────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────┐
│  Codapter.Web (ASP.NET MVC)                                 │
│  ──────────────────────────                                 │
│  • RpcController — JSON-RPC over HTTP POST                  │
│  • WebSocketRpcMiddleware — NDJSON over WebSocket            │
│  • RpcHub — SignalR real-time hub                           │
│  • HomeController — MVC dashboard views                     │
│  • HealthController — /healthz, /readyz                     │
│  • AppServerConnection — request router & state manager     │
└──────────────┬──────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────┐
│  Codapter.Core (Class Library)                              │
│  ────────────────────────────                               │
│  Models:                                                    │
│  • Protocol — Codex protocol types                          │
│  • JsonRpc — JSON-RPC 2.0 types & helpers                   │
│  • BackendTypes — IBackend interface & events               │
│  • CollabTypes — collaboration agent types                  │
│                                                             │
│  Services:                                                  │
│  • ThreadRegistry — persistent thread metadata              │
│  • TurnStateMachine — event → notification decomposition    │
│  • CommandExecManager — native shell execution              │
│  • ConfigStore — settings with version control              │
│  • CollabManager — sub-agent orchestration                  │
│  • ToolItems — tool classification & diff synthesis         │
└──────────────┬──────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────┐
│  Codapter.BackendPi (Class Library)                         │
│  ─────────────────────────────────                          │
│  • PiBackend — IBackend implementation                      │
│  • PiProcessSession — JSON-RPC stdio communication          │
│  • PiStateStore — persistent session state                  │
│  • JsonlStream — JSONL stream processing                    │
└──────────────┬──────────────────────────────────────────────┘
               │
┌──────────────▼──────────────────────────────────────────────┐
│  Pi Coding Agent (@mariozechner/pi-coding-agent)            │
│  → Anthropic, OpenAI, Google, Mistral, etc.                 │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
codapter-csharp/
├── Codapter.sln
├── src/
│   ├── Codapter.Core/           # Core types, interfaces, services
│   │   ├── Models/              # Protocol, JsonRpc, Backend, Collab types
│   │   ├── Services/            # ThreadRegistry, TurnStateMachine, etc.
│   │   └── Utilities/           # NdJson helpers
│   ├── Codapter.BackendPi/      # Pi backend implementation
│   │   ├── PiBackend.cs         # IBackend implementation
│   │   ├── PiProcessSession.cs  # Subprocess JSON-RPC communication
│   │   ├── PiStateStore.cs      # Persistent session state
│   │   └── JsonlStream.cs       # JSONL stream utilities
│   └── Codapter.Web/            # ASP.NET MVC application
│       ├── Controllers/         # MVC + API controllers
│       ├── Hubs/                # SignalR hub
│       ├── Middleware/           # WebSocket NDJSON middleware
│       ├── Services/            # AppServerConnection
│       └── Views/               # Razor views
└── tests/
    └── Codapter.Tests/          # xUnit test suite
```

## Requirements

- .NET 8.0 SDK
- Node.js 22+ (for Pi backend subprocess)

## Setup

```bash
# Clone and build
cd codapter-csharp
dotnet restore
dotnet build

# Run tests
dotnet test

# Run the web application
cd src/Codapter.Web
dotnet run
```

## Configuration

Environment variables (matching the TypeScript version):

| Variable | Purpose | Default |
|----------|---------|---------|
| `CODAPTER_COLLAB` | Enable sub-agent collaboration | Disabled |
| `CODAPTER_PI_COMMAND` | Pi launch command | `npx` |
| `CODAPTER_PI_IDLE_TIMEOUT_MS` | Idle session timeout | `300000` (5 min) |
| `CODAPTER_DEBUG_LOG_FILE` | Debug JSONL log path | Disabled |

## Transport Endpoints

| Endpoint | Transport | Description |
|----------|-----------|-------------|
| `POST /api/rpc` | HTTP | JSON-RPC over HTTP |
| `POST /api/rpc/batch` | HTTP | Batch JSON-RPC |
| `ws://host/ws/rpc` | WebSocket | NDJSON-RPC over WebSocket |
| `/hub/rpc` | SignalR | Real-time RPC via SignalR |
| `GET /healthz` | HTTP | Health check |
| `GET /readyz` | HTTP | Readiness check |

## Supported RPC Methods

### Fully Implemented
- **Threads**: `thread/start`, `thread/resume`, `thread/fork`, `thread/list`, `thread/archive`
- **Turns**: `turn/start`, `turn/interrupt`
- **Models**: `model/list`, `model/current`
- **Config**: `config/read`, `config/value/write`
- **Commands**: `command/exec`, `command/exec/stdin`, `command/exec/interrupt`
- **Auth**: `auth/status`, `auth/login`, `auth/logout`

### Stubbed
- `collaborationMode/list` → empty array
- `experimentalFeature/list` → empty array
- `mcpServerStatus/list` → empty array

## Mapping from TypeScript

| TypeScript Package | C# Project |
|---|---|
| `packages/core/` | `Codapter.Core` |
| `packages/backend-pi/` | `Codapter.BackendPi` |
| `packages/cli/` | `Codapter.Web` (MVC replaces CLI) |
| `packages/collab-extension/` | `Codapter.Core.Services.CollabManager` |

## License

MIT — Same as the original codapter project.
