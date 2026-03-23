using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Codapter.Core.Models;
using Microsoft.Extensions.Logging;

namespace Codapter.Core.Services;

/// <summary>
/// Manages execution of child processes with streaming I/O support.
/// Port of packages/core/src/command-exec.ts
/// </summary>

public record CommandExecOptions
{
    public string Command { get; init; } = default!;
    public List<string>? Args { get; init; }
    public string? Cwd { get; init; }
    public Dictionary<string, string>? Env { get; init; }
    public int? TimeoutMs { get; init; }
    public bool Streaming { get; init; }
    public string? ProcessId { get; init; }
    public long MaxOutputBytes { get; init; } = 1_048_576; // 1MB default
}

internal class RunningProcess : IDisposable
{
    public string ProcessId { get; init; } = default!;
    public Process Process { get; init; } = default!;
    public bool SupportsStdin { get; init; }
    public bool IsStreaming { get; init; }
    public TaskCompletionSource<CommandExecResponse> Completion { get; } = new();
    public CancellationTokenSource TimeoutCts { get; } = new();

    public void Dispose()
    {
        TimeoutCts.Dispose();
        try { Process.Kill(entireProcessTree: true); } catch { }
        Process.Dispose();
    }
}

public class CommandExecManager : IDisposable
{
    private readonly ConcurrentDictionary<string, RunningProcess> _running = new();
    private readonly ILogger<CommandExecManager>? _logger;
    private readonly Action<string, string, string>? _streamingCallback; // processId, stream, data

    public CommandExecManager(
        ILogger<CommandExecManager>? logger = null,
        Action<string, string, string>? streamingCallback = null)
    {
        _logger = logger;
        _streamingCallback = streamingCallback;
    }

    public async Task<CommandExecResponse> ExecuteAsync(
        CommandExecOptions options, CancellationToken ct = default)
    {
        var processId = options.ProcessId ?? Guid.NewGuid().ToString();

        var startInfo = new ProcessStartInfo
        {
            FileName = GetShell(),
            Arguments = GetShellArgs(options.Command, options.Args),
            WorkingDirectory = options.Cwd ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Merge environment variables
        if (options.Env is not null)
        {
            foreach (var (key, value) in options.Env)
            {
                startInfo.EnvironmentVariables[key] = value;
            }
        }

        var process = new Process { StartInfo = startInfo };
        var running = new RunningProcess
        {
            ProcessId = processId,
            Process = process,
            SupportsStdin = true,
            IsStreaming = options.Streaming
        };

        _running[processId] = running;

        try
        {
            process.Start();

            // Set up timeout
            if (options.TimeoutMs.HasValue)
            {
                running.TimeoutCts.CancelAfter(options.TimeoutMs.Value);
            }

            var stdoutBuilder = new StringBuilder();
            var stderrBuilder = new StringBuilder();
            long totalBytes = 0;
            var maxBytes = options.MaxOutputBytes;

            var stdoutTask = ReadStreamAsync(
                process.StandardOutput,
                stdoutBuilder,
                "stdout",
                processId,
                options.Streaming,
                maxBytes,
                ref totalBytes,
                running.TimeoutCts.Token);

            var stderrTask = ReadStreamAsync(
                process.StandardError,
                stderrBuilder,
                "stderr",
                processId,
                options.Streaming,
                maxBytes,
                ref totalBytes,
                running.TimeoutCts.Token);

            // Wait for process exit or timeout
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, running.TimeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
                await Task.WhenAll(stdoutTask, stderrTask);
            }
            catch (OperationCanceledException) when (running.TimeoutCts.IsCancellationRequested)
            {
                // Timeout - kill process
                try { process.Kill(entireProcessTree: true); } catch { }

                return new CommandExecResponse
                {
                    ProcessId = processId,
                    ExitCode = 124, // Standard timeout exit code
                    Stdout = stdoutBuilder.ToString(),
                    Stderr = stderrBuilder.ToString()
                };
            }

            var response = new CommandExecResponse
            {
                ProcessId = processId,
                ExitCode = process.ExitCode,
                Stdout = options.Streaming ? null : stdoutBuilder.ToString(),
                Stderr = options.Streaming ? null : stderrBuilder.ToString()
            };

            running.Completion.TrySetResult(response);
            return response;
        }
        finally
        {
            _running.TryRemove(processId, out _);
        }
    }

    private async Task ReadStreamAsync(
        StreamReader reader,
        StringBuilder buffer,
        string streamName,
        string processId,
        bool streaming,
        long maxBytes,
        ref long totalBytes,
        CancellationToken ct)
    {
        var charBuffer = new char[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var read = await reader.ReadAsync(charBuffer, ct);
                if (read == 0) break;

                var text = new string(charBuffer, 0, read);
                var byteCount = Encoding.UTF8.GetByteCount(text);

                if (Interlocked.Read(ref totalBytes) + byteCount > maxBytes)
                {
                    _logger?.LogWarning("Output cap reached for process {ProcessId}", processId);
                    break;
                }

                Interlocked.Add(ref totalBytes, byteCount);
                buffer.Append(text);

                if (streaming)
                {
                    var base64Data = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
                    _streamingCallback?.Invoke(processId, streamName, base64Data);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on timeout
        }
    }

    public async Task WriteStdinAsync(string processId, string data, CancellationToken ct = default)
    {
        if (!_running.TryGetValue(processId, out var running))
            throw new InvalidOperationException($"Process {processId} not found");

        if (!running.SupportsStdin)
            throw new InvalidOperationException($"Process {processId} does not support stdin");

        await running.Process.StandardInput.WriteAsync(data.AsMemory(), ct);
        await running.Process.StandardInput.FlushAsync(ct);
    }

    public void Terminate(string processId)
    {
        if (_running.TryGetValue(processId, out var running))
        {
            try { running.Process.Kill(entireProcessTree: true); } catch { }
        }
    }

    public void Dispose()
    {
        foreach (var (_, running) in _running)
        {
            running.Dispose();
        }
        _running.Clear();
    }

    private static string GetShell()
    {
        if (OperatingSystem.IsWindows())
            return "cmd.exe";
        return Environment.GetEnvironmentVariable("SHELL") ?? "/bin/sh";
    }

    private static string GetShellArgs(string command, List<string>? args)
    {
        var fullCommand = args is { Count: > 0 }
            ? $"{command} {string.Join(' ', args)}"
            : command;

        if (OperatingSystem.IsWindows())
            return $"/c {fullCommand}";
        return $"-c \"{fullCommand.Replace("\"", "\\\"")}\"";
    }
}
