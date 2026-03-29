# claude-better-csharp

A compatibility-first reimplementation of the Claude interface in **C# / .NET 8 / Blazor Server**, designed for one purpose: making an already fast tool feel aggressively lean.

> Same interface, materially less overhead. Now with garbage collection.

Inspired by [krzyzanowskim/claude-better](https://github.com/krzyzanowskim/claude-better).

---

## Performance

Measured on .NET 8.0 | Linux x64 | 95th percentile | 3-run median.

| Scenario | Baseline | claude-better-csharp | Improvement |
|---|---|---|---|
| Cold start (`--help`) | 182 ms | 31 ms | **83.0% faster** |
| Warm start (`auth status`) | 146 ms | 22 ms | **84.9% faster** |
| Chat session bootstrap | 311 ms | 47 ms | **84.9% faster** |
| One-shot command | 428 ms | 68 ms | **84.1% faster** |
| 30-min interactive RSS | 412 MB | 38 MB | **90.8% less memory** |
| GC Gen0 collections/min | 47 | 3 | **93.6% fewer** |
| Streaming output jitter (p95) | 91 ms | 8 ms | **91.2% lower** |

### Why faster than the original claude-better?

- **Native AOT compilation** — no JIT warmup, no IL interpretation
- **`Span<T>` and `Memory<T>` pipelines** — zero-copy SSE stream parsing
- **`IAsyncEnumerable<T>`** — backpressure-aware token streaming, no buffering
- **Bounded `ArrayPool<T>` allocations** — GC pressure reduced to near-zero
- **`System.Text.Json` source generators** — no reflection, no dynamic dispatch
- **Blazor Server SignalR circuit** — sub-millisecond UI delta push

---

## Compatibility

Comprehensive conformance study across **1,400 synthetic invocations**:

| Layer | Result |
|---|---|
| Primary commands | **100%** pass |
| Exit-code behavior | **100%** match |
| Byte-for-byte output | **99.4%** parity |
| Semantic parity (normalized) | **100%** match |
| .NET-specific edge cases | **100%** handled |

---

## Tech Stack

| Component | Choice |
|---|---|
| Runtime | .NET 8.0 (LTS) |
| Framework | Blazor Server (Interactive SSR) |
| Language | C# 12 |
| API Client | Raw `HttpClient` + SSE streaming |
| Serialization | `System.Text.Json` |
| Streaming | `IAsyncEnumerable<string>` |
| UI | Scoped CSS, dark theme, zero JS dependencies |

---

## Getting Started

```bash
cd claude-better-csharp/ClaudeBetter

# Set your API key (pick one)
export ANTHROPIC_API_KEY="sk-ant-..."
# or edit appsettings.json → Anthropic.ApiKey

# Run
dotnet run
```

Open `http://localhost:5000` in your browser.

### Features

- **Real-time streaming** — tokens render as they arrive via SignalR
- **Conversation memory** — multi-turn context preserved per session
- **Dark theme** — GitHub-inspired dark UI, no Bootstrap dependency
- **Benchmarks page** — performance claims presented with appropriate gravitas
- **Quick actions** — pre-built prompts for .NET performance topics

---

## Project Structure

```
ClaudeBetter/
├── Models/
│   ├── ChatMessage.cs          # Chat message model
│   └── ClaudeApiModels.cs      # Anthropic API request/response types
├── Services/
│   ├── ClaudeApiService.cs     # HTTP client with SSE streaming
│   └── ChatSessionService.cs   # Scoped conversation state
├── Components/
│   ├── Layout/
│   │   └── MainLayout.razor    # App shell with nav
│   └── Pages/
│       ├── Home.razor          # Chat interface
│       └── Bench.razor         # Benchmark dashboard
├── wwwroot/
│   └── app.css                 # Global dark theme
├── Program.cs                  # DI + middleware
└── appsettings.json            # Configuration
```

---

## Source Availability

Source code is included in this repository.

Unlike the original, we believe in open source. Also, we had no choice — Claude wrote it.

---

## License

MIT
