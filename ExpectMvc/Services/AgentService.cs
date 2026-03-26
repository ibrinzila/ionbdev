using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExpectMvc.Models;

namespace ExpectMvc.Services;

public class AgentService : IAgentService
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<AgentService> _logger;

    // Session store: sessionId → conversation messages for resumption
    private static readonly ConcurrentDictionary<string, List<JsonObject>> Sessions = new();
    private string? _lastSessionId;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // Tool definitions the agent can call during planning
    private static readonly JsonArray ToolDefinitions = new()
    {
        new JsonObject
        {
            ["name"] = "read_file",
            ["description"] = "Read the contents of a file at the given path in the repository.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["path"] = new JsonObject { ["type"] = "string", ["description"] = "Absolute or repo-relative file path" }
                },
                ["required"] = new JsonArray { "path" }
            }
        },
        new JsonObject
        {
            ["name"] = "run_command",
            ["description"] = "Execute a shell command in the repository working directory. Use for listing files, checking structure, running grep, etc.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["command"] = new JsonObject { ["type"] = "string", ["description"] = "The shell command to run" }
                },
                ["required"] = new JsonArray { "command" }
            }
        },
        new JsonObject
        {
            ["name"] = "search_code",
            ["description"] = "Search for a pattern in the codebase using grep. Returns matching lines with file paths.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["pattern"] = new JsonObject { ["type"] = "string", ["description"] = "Regex pattern to search for" },
                    ["glob"] = new JsonObject { ["type"] = "string", ["description"] = "Optional file glob filter, e.g. '*.cs'" }
                },
                ["required"] = new JsonArray { "pattern" }
            }
        },
        new JsonObject
        {
            ["name"] = "list_files",
            ["description"] = "List files in a directory, optionally with a glob pattern.",
            ["input_schema"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["directory"] = new JsonObject { ["type"] = "string", ["description"] = "Directory path" },
                    ["pattern"] = new JsonObject { ["type"] = "string", ["description"] = "Optional glob pattern" }
                },
                ["required"] = new JsonArray { "directory" }
            }
        }
    };

    public AgentService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<AgentService> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public string? GetLastSessionId() => _lastSessionId;

    public async Task<TestPlan> GeneratePlanAsync(GitDiff diff, AgentProvider provider, string? message = null, string? sessionId = null)
    {
        _logger.LogInformation("Generating test plan with {Provider} (session={Session})", provider, sessionId ?? "new");

        var steps = provider switch
        {
            AgentProvider.Claude => await GenerateWithClaudeToolLoopAsync(diff, message, sessionId),
            AgentProvider.Codex => await GenerateWithCodexToolLoopAsync(diff, message, sessionId),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        return new TestPlan
        {
            Description = message ?? $"Auto-generated plan for {diff.Files.Count} changed file(s)",
            Steps = steps,
            SourceDiff = diff,
            Agent = provider
        };
    }

    public async IAsyncEnumerable<AgentStreamEvent> GeneratePlanStreamAsync(
        GitDiff diff, AgentProvider provider, string? message = null, string? sessionId = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var repoPath = diff.Repository;
        var prompt = BuildPrompt(diff, message);
        var currentSessionId = sessionId ?? Guid.NewGuid().ToString("N");
        _lastSessionId = currentSessionId;

        var messages = sessionId != null && Sessions.TryGetValue(sessionId, out var existing)
            ? new List<JsonObject>(existing)
            : new List<JsonObject>();

        if (messages.Count == 0)
        {
            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = prompt
            });
        }
        else
        {
            messages.Add(new JsonObject
            {
                ["role"] = "user",
                ["content"] = message ?? "Continue generating the test plan."
            });
        }

        var apiKey = provider == AgentProvider.Claude
            ? _config["Expect:AnthropicApiKey"]
            : _config["Expect:OpenAiApiKey"];

        if (string.IsNullOrEmpty(apiKey))
        {
            yield return new AgentStreamEvent { Type = AgentStreamEventType.Error, Data = $"{provider} API key not configured" };
            yield break;
        }

        const int maxIterations = 15;
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            if (ct.IsCancellationRequested) yield break;

            JsonDocument response;
            if (provider == AgentProvider.Claude)
                response = await CallClaudeAsync(apiKey, messages);
            else
                response = await CallCodexAsync(apiKey, messages);

            var root = response.RootElement;

            // Check if there are tool calls
            var hasToolUse = false;
            var assistantContent = new JsonArray();
            var textAccum = new StringBuilder();

            if (provider == AgentProvider.Claude)
            {
                var stopReason = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : "";
                var content = root.GetProperty("content");

                foreach (var block in content.EnumerateArray())
                {
                    var blockType = block.GetProperty("type").GetString();

                    if (blockType == "thinking" && block.TryGetProperty("thinking", out var thinking))
                    {
                        var thinkingText = thinking.GetString() ?? "";
                        yield return new AgentStreamEvent { Type = AgentStreamEventType.Reasoning, Data = thinkingText };
                        assistantContent.Add(JsonNode.Parse(block.GetRawText())!);
                    }
                    else if (blockType == "text")
                    {
                        var text = block.GetProperty("text").GetString() ?? "";
                        textAccum.Append(text);
                        yield return new AgentStreamEvent { Type = AgentStreamEventType.Text, Data = text };
                        assistantContent.Add(JsonNode.Parse(block.GetRawText())!);
                    }
                    else if (blockType == "tool_use")
                    {
                        hasToolUse = true;
                        var toolName = block.GetProperty("name").GetString() ?? "";
                        var toolId = block.GetProperty("id").GetString() ?? "";
                        var toolInput = block.GetProperty("input");

                        yield return new AgentStreamEvent
                        {
                            Type = AgentStreamEventType.ToolCall,
                            Data = $"{toolName}: {toolInput.GetRawText()}"
                        };

                        assistantContent.Add(JsonNode.Parse(block.GetRawText())!);

                        // Execute the tool
                        var toolResult = await ExecuteToolAsync(toolName, toolInput, repoPath);

                        yield return new AgentStreamEvent
                        {
                            Type = AgentStreamEventType.ToolResult,
                            Data = toolResult.Length > 500 ? toolResult[..500] + "..." : toolResult
                        };

                        // Add assistant message with all content so far, then tool result
                        messages.Add(new JsonObject
                        {
                            ["role"] = "assistant",
                            ["content"] = JsonNode.Parse(assistantContent.ToJsonString())
                        });
                        assistantContent = new JsonArray();

                        messages.Add(new JsonObject
                        {
                            ["role"] = "user",
                            ["content"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["type"] = "tool_result",
                                    ["tool_use_id"] = toolId,
                                    ["content"] = toolResult
                                }
                            }
                        });
                    }
                }

                if (!hasToolUse)
                {
                    // Final response — add to messages and save session
                    if (assistantContent.Count > 0)
                    {
                        messages.Add(new JsonObject
                        {
                            ["role"] = "assistant",
                            ["content"] = JsonNode.Parse(assistantContent.ToJsonString())
                        });
                    }
                    Sessions[currentSessionId] = messages;

                    yield return new AgentStreamEvent
                    {
                        Type = AgentStreamEventType.PlanReady,
                        Data = textAccum.ToString()
                    };
                    yield break;
                }
            }
            else
            {
                // OpenAI format with function calling
                var choices = root.GetProperty("choices");
                if (choices.GetArrayLength() == 0) yield break;

                var choice = choices[0];
                var msg = choice.GetProperty("message");
                var finishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : "";

                if (msg.TryGetProperty("content", out var contentEl) && contentEl.ValueKind == JsonValueKind.String)
                {
                    var text = contentEl.GetString() ?? "";
                    textAccum.Append(text);
                    yield return new AgentStreamEvent { Type = AgentStreamEventType.Text, Data = text };
                }

                if (msg.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
                {
                    hasToolUse = true;
                    messages.Add(JsonNode.Parse(msg.GetRawText())!.AsObject());

                    foreach (var tc in toolCalls.EnumerateArray())
                    {
                        var tcId = tc.GetProperty("id").GetString() ?? "";
                        var fn = tc.GetProperty("function");
                        var toolName = fn.GetProperty("name").GetString() ?? "";
                        var argsStr = fn.GetProperty("arguments").GetString() ?? "{}";
                        var toolInput = JsonDocument.Parse(argsStr).RootElement;

                        yield return new AgentStreamEvent
                        {
                            Type = AgentStreamEventType.ToolCall,
                            Data = $"{toolName}: {argsStr}"
                        };

                        var toolResult = await ExecuteToolAsync(toolName, toolInput, repoPath);

                        yield return new AgentStreamEvent
                        {
                            Type = AgentStreamEventType.ToolResult,
                            Data = toolResult.Length > 500 ? toolResult[..500] + "..." : toolResult
                        };

                        messages.Add(new JsonObject
                        {
                            ["role"] = "tool",
                            ["tool_call_id"] = tcId,
                            ["content"] = toolResult
                        });
                    }
                }

                if (!hasToolUse)
                {
                    messages.Add(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = textAccum.ToString()
                    });
                    Sessions[currentSessionId] = messages;

                    yield return new AgentStreamEvent
                    {
                        Type = AgentStreamEventType.PlanReady,
                        Data = textAccum.ToString()
                    };
                    yield break;
                }
            }
        }

        yield return new AgentStreamEvent
        {
            Type = AgentStreamEventType.Error,
            Data = "Max tool iterations reached without completing the plan"
        };
    }

    // --- Tool-loop implementations for non-streaming ---

    private async Task<List<TestStep>> GenerateWithClaudeToolLoopAsync(GitDiff diff, string? message, string? sessionId)
    {
        var apiKey = _config["Expect:AnthropicApiKey"]
            ?? throw new InvalidOperationException("AnthropicApiKey is not configured.");

        var prompt = BuildPrompt(diff, message);
        var currentSessionId = sessionId ?? Guid.NewGuid().ToString("N");
        _lastSessionId = currentSessionId;

        var messages = sessionId != null && Sessions.TryGetValue(sessionId, out var existing)
            ? new List<JsonObject>(existing)
            : new List<JsonObject>();

        if (messages.Count == 0)
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = prompt });
        }
        else
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = message ?? "Continue." });
        }

        const int maxIterations = 15;
        for (var i = 0; i < maxIterations; i++)
        {
            var response = await CallClaudeAsync(apiKey, messages);
            var root = response.RootElement;
            var content = root.GetProperty("content");
            var stopReason = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : "";

            var hasToolUse = false;
            var textAccum = new StringBuilder();
            var assistantContent = new JsonArray();

            foreach (var block in content.EnumerateArray())
            {
                var blockType = block.GetProperty("type").GetString();
                assistantContent.Add(JsonNode.Parse(block.GetRawText())!);

                if (blockType == "text")
                {
                    textAccum.Append(block.GetProperty("text").GetString() ?? "");
                }
                else if (blockType == "tool_use")
                {
                    hasToolUse = true;
                    var toolName = block.GetProperty("name").GetString() ?? "";
                    var toolId = block.GetProperty("id").GetString() ?? "";
                    var toolInput = block.GetProperty("input");

                    _logger.LogInformation("Agent tool call: {Tool} {Input}", toolName, toolInput.GetRawText());

                    var toolResult = await ExecuteToolAsync(toolName, toolInput, diff.Repository);

                    messages.Add(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = JsonNode.Parse(assistantContent.ToJsonString())
                    });
                    assistantContent = new JsonArray();

                    messages.Add(new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = new JsonArray
                        {
                            new JsonObject
                            {
                                ["type"] = "tool_result",
                                ["tool_use_id"] = toolId,
                                ["content"] = toolResult
                            }
                        }
                    });
                }
            }

            if (!hasToolUse)
            {
                if (assistantContent.Count > 0)
                {
                    messages.Add(new JsonObject
                    {
                        ["role"] = "assistant",
                        ["content"] = JsonNode.Parse(assistantContent.ToJsonString())
                    });
                }
                Sessions[currentSessionId] = messages;
                return ParseStepsFromText(textAccum.ToString());
            }
        }

        _logger.LogWarning("Claude agent hit max iterations ({Max})", maxIterations);
        Sessions[currentSessionId] = messages;
        return new List<TestStep>();
    }

    private async Task<List<TestStep>> GenerateWithCodexToolLoopAsync(GitDiff diff, string? message, string? sessionId)
    {
        var apiKey = _config["Expect:OpenAiApiKey"]
            ?? throw new InvalidOperationException("OpenAiApiKey is not configured.");

        var prompt = BuildPrompt(diff, message);
        var currentSessionId = sessionId ?? Guid.NewGuid().ToString("N");
        _lastSessionId = currentSessionId;

        var messages = sessionId != null && Sessions.TryGetValue(sessionId, out var existing)
            ? new List<JsonObject>(existing)
            : new List<JsonObject>();

        if (messages.Count == 0)
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = prompt });
        }
        else
        {
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = message ?? "Continue." });
        }

        const int maxIterations = 15;
        for (var i = 0; i < maxIterations; i++)
        {
            var response = await CallCodexAsync(apiKey, messages);
            var root = response.RootElement;
            var choice = root.GetProperty("choices")[0];
            var msg = choice.GetProperty("message");

            var text = msg.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString() ?? "" : "";

            if (msg.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.GetArrayLength() > 0)
            {
                messages.Add(JsonNode.Parse(msg.GetRawText())!.AsObject());

                foreach (var tc in toolCalls.EnumerateArray())
                {
                    var tcId = tc.GetProperty("id").GetString() ?? "";
                    var fn = tc.GetProperty("function");
                    var toolName = fn.GetProperty("name").GetString() ?? "";
                    var argsStr = fn.GetProperty("arguments").GetString() ?? "{}";
                    var toolInput = JsonDocument.Parse(argsStr).RootElement;

                    _logger.LogInformation("Agent tool call: {Tool} {Input}", toolName, argsStr);
                    var toolResult = await ExecuteToolAsync(toolName, toolInput, diff.Repository);

                    messages.Add(new JsonObject
                    {
                        ["role"] = "tool",
                        ["tool_call_id"] = tcId,
                        ["content"] = toolResult
                    });
                }
            }
            else
            {
                messages.Add(new JsonObject { ["role"] = "assistant", ["content"] = text });
                Sessions[currentSessionId] = messages;
                return ParseStepsFromText(text);
            }
        }

        Sessions[currentSessionId] = messages;
        return new List<TestStep>();
    }

    // --- API call helpers ---

    private async Task<JsonDocument> CallClaudeAsync(string apiKey, List<JsonObject> messages)
    {
        var http = _httpFactory.CreateClient("Claude");
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", "2024-10-22");

        var body = new JsonObject
        {
            ["model"] = "claude-sonnet-4-20250514",
            ["max_tokens"] = 4096,
            ["tools"] = JsonNode.Parse(ToolDefinitions.ToJsonString()),
            ["messages"] = JsonNode.Parse(JsonSerializer.Serialize(messages))
        };

        var response = await http.PostAsync(
            "https://api.anthropic.com/v1/messages",
            new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    private async Task<JsonDocument> CallCodexAsync(string apiKey, List<JsonObject> messages)
    {
        var http = _httpFactory.CreateClient("OpenAI");
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // Convert our tool definitions to OpenAI function-calling format
        var openAiTools = new JsonArray();
        foreach (var tool in ToolDefinitions)
        {
            openAiTools.Add(new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = tool!.AsObject()["name"]!.DeepClone(),
                    ["description"] = tool.AsObject()["description"]!.DeepClone(),
                    ["parameters"] = tool.AsObject()["input_schema"]!.DeepClone()
                }
            });
        }

        var body = new JsonObject
        {
            ["model"] = "gpt-4o",
            ["max_tokens"] = 4096,
            ["tools"] = openAiTools,
            ["messages"] = JsonNode.Parse(JsonSerializer.Serialize(messages))
        };

        var response = await http.PostAsync(
            "https://api.openai.com/v1/chat/completions",
            new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json);
    }

    // --- Tool execution ---

    private async Task<string> ExecuteToolAsync(string toolName, JsonElement input, string repoPath)
    {
        try
        {
            return toolName switch
            {
                "read_file" => await ExecuteReadFileAsync(input, repoPath),
                "run_command" => await ExecuteCommandAsync(input, repoPath),
                "search_code" => await ExecuteSearchAsync(input, repoPath),
                "list_files" => await ExecuteListFilesAsync(input, repoPath),
                _ => $"Unknown tool: {toolName}"
            };
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static async Task<string> ExecuteReadFileAsync(JsonElement input, string repoPath)
    {
        var path = input.GetProperty("path").GetString() ?? "";
        if (!Path.IsPathRooted(path))
            path = Path.Combine(repoPath, path);

        if (!File.Exists(path))
            return $"File not found: {path}";

        var content = await File.ReadAllTextAsync(path);
        // Limit to avoid blowing up context
        return content.Length > 10000 ? content[..10000] + "\n... (truncated at 10000 chars)" : content;
    }

    private static async Task<string> ExecuteCommandAsync(JsonElement input, string repoPath)
    {
        var command = input.GetProperty("command").GetString() ?? "";

        // Safety: block destructive commands
        var lower = command.ToLowerInvariant();
        if (lower.Contains("rm -rf") || lower.Contains("mkfs") || lower.Contains("dd if=") ||
            lower.Contains("> /dev/") || lower.Contains("chmod 777") || lower.Contains("curl") && lower.Contains("|") && lower.Contains("sh"))
        {
            return "Blocked: potentially destructive command";
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command.Replace("\"", "\\\"")}\"",
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var output = await process.StandardOutput.ReadToEndAsync(cts.Token);
        var error = await process.StandardError.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token);

        var result = string.IsNullOrEmpty(error) ? output : $"{output}\nSTDERR: {error}";
        return result.Length > 8000 ? result[..8000] + "\n... (truncated)" : result;
    }

    private static async Task<string> ExecuteSearchAsync(JsonElement input, string repoPath)
    {
        var pattern = input.GetProperty("pattern").GetString() ?? "";
        var glob = input.TryGetProperty("glob", out var g) ? g.GetString() ?? "" : "";

        var args = $"-rn --max-count=50";
        if (!string.IsNullOrEmpty(glob))
            args += $" --include=\"{glob}\"";
        args += $" \"{pattern}\" .";

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "grep",
                Arguments = args,
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return string.IsNullOrEmpty(output) ? "No matches found" : output;
    }

    private static async Task<string> ExecuteListFilesAsync(JsonElement input, string repoPath)
    {
        var dir = input.GetProperty("directory").GetString() ?? ".";
        if (!Path.IsPathRooted(dir))
            dir = Path.Combine(repoPath, dir);

        if (!Directory.Exists(dir))
            return $"Directory not found: {dir}";

        var pattern = input.TryGetProperty("pattern", out var p) ? p.GetString() ?? "*" : "*";
        var entries = Directory.GetFileSystemEntries(dir, pattern, SearchOption.TopDirectoryOnly);

        var sb = new StringBuilder();
        foreach (var entry in entries.OrderBy(e => e).Take(100))
        {
            var isDir = Directory.Exists(entry);
            var name = Path.GetFileName(entry);
            sb.AppendLine(isDir ? $"{name}/" : name);
        }

        if (entries.Length > 100)
            sb.AppendLine($"... and {entries.Length - 100} more");

        return sb.ToString();
    }

    // --- Prompt building ---

    private static string BuildPrompt(GitDiff diff, string? message)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a QA engineer with access to the codebase. Analyze the code changes and generate a browser test plan.");
        sb.AppendLine("You have tools available to explore the codebase: read_file, run_command, search_code, list_files.");
        sb.AppendLine("USE THESE TOOLS to understand the codebase context before generating the plan.");
        sb.AppendLine("For example, read the changed files in full, look at related tests, and understand the app structure.");
        sb.AppendLine();
        sb.AppendLine("Each test step will be executed against a live page using accessibility-based element targeting.");
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: For the 'selector' field, use DESCRIPTIVE role+name pairs that match");
        sb.AppendLine("accessibility tree elements, like: 'button:Sign In', 'textbox:Email', 'link:Home'.");
        sb.AppendLine("Use format 'role:name'. Use null for navigation/wait steps.");
        sb.AppendLine();
        sb.AppendLine("For the 'action' field, use one of: click, fill, type, check, uncheck, select, hover, focus, navigate.");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(message))
        {
            sb.AppendLine($"User instruction: {message}");
            sb.AppendLine();
        }

        sb.AppendLine($"Repository: {diff.Repository}");
        sb.AppendLine($"Branch: {diff.Branch}");
        sb.AppendLine($"Files changed: {diff.Files.Count}");
        sb.AppendLine();

        foreach (var file in diff.Files)
        {
            sb.AppendLine($"--- {file.Path} ({file.Type}) +{file.Additions} -{file.Deletions} ---");
            if (!string.IsNullOrEmpty(file.Patch))
            {
                var patch = file.Patch.Length > 3000 ? file.Patch[..3000] + "\n... (truncated)" : file.Patch;
                sb.AppendLine(patch);
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("After exploring the codebase with tools, respond with your final test plan as ONLY a JSON array of objects:");
        sb.AppendLine("  { \"order\": number, \"action\": string, \"selector\": \"role:name\" | null, \"value\": string | null, \"expected\": string }");

        return sb.ToString();
    }

    // --- Response parsing ---

    private static List<TestStep> ParseStepsFromText(string text)
    {
        var jsonStart = text.IndexOf('[');
        var jsonEnd = text.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd < 0)
            return new List<TestStep>();

        var stepsJson = text[jsonStart..(jsonEnd + 1)];
        var steps = JsonSerializer.Deserialize<List<TestStep>>(stepsJson, JsonOpts);
        return steps ?? new List<TestStep>();
    }
}
