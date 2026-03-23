using System.Text.Json;
using System.Text.Json.Serialization;
using Codapter.Core.Models;

namespace Codapter.Core.Services;

/// <summary>
/// Decomposes backend streaming events into Codex GUI notifications.
/// Port of packages/core/src/turn-state.ts
/// </summary>

public record TurnNotification
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = default!;

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Params { get; init; }
}

public class TurnStateMachine
{
    private readonly string _threadId;
    private readonly string _turnId;
    private readonly Action<TurnNotification> _notificationSink;
    private readonly bool _collabEnabled;

    private string? _currentAgentMessageItemId;
    private string? _currentReasoningItemId;
    private string _agentMessageText = "";
    private string _reasoningSummaryText = "";

    // Track active tools: toolCallId -> itemId
    private readonly Dictionary<string, string> _activeTools = new();
    // Track cumulative tool output for delta computation
    private readonly Dictionary<string, string> _toolOutputCumulative = new();

    private readonly List<ThreadItem> _items = new();
    private ThreadTokenUsage? _usage;

    public IReadOnlyList<ThreadItem> Items => _items;
    public ThreadTokenUsage? Usage => _usage;

    public TurnStateMachine(
        string threadId,
        string turnId,
        Action<TurnNotification> notificationSink,
        bool collabEnabled = false)
    {
        _threadId = threadId;
        _turnId = turnId;
        _notificationSink = notificationSink;
        _collabEnabled = collabEnabled;
    }

    public void HandleEvent(BackendEvent evt)
    {
        switch (evt.Type)
        {
            case BackendEventType.TextDelta:
                HandleTextDelta(evt.Text ?? "");
                break;
            case BackendEventType.ThinkingDelta:
                HandleThinkingDelta(evt.Text ?? "");
                break;
            case BackendEventType.ToolStart:
                HandleToolStart(evt);
                break;
            case BackendEventType.ToolUpdate:
                HandleToolUpdate(evt);
                break;
            case BackendEventType.ToolEnd:
                HandleToolEnd(evt);
                break;
            case BackendEventType.MessageEnd:
                HandleMessageEnd();
                break;
            case BackendEventType.Error:
                HandleError(evt.Error ?? "Unknown error");
                break;
            case BackendEventType.TokenUsage:
                HandleTokenUsage(evt.Usage);
                break;
        }
    }

    private void HandleTextDelta(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (_currentAgentMessageItemId == null)
        {
            _currentAgentMessageItemId = Guid.NewGuid().ToString();
            var item = new ThreadItem
            {
                Id = _currentAgentMessageItemId,
                Type = ThreadItemType.AgentMessage,
                Text = "",
                ItemStatus = "in_progress"
            };
            _items.Add(item);

            Notify("item/started", new
            {
                threadId = _threadId,
                turnId = _turnId,
                item
            });
        }

        _agentMessageText += text;
        Notify("item/agentMessage/delta", new
        {
            threadId = _threadId,
            turnId = _turnId,
            itemId = _currentAgentMessageItemId,
            textDelta = text
        });
    }

    private void HandleThinkingDelta(string text)
    {
        if (string.IsNullOrEmpty(text)) return;

        if (_currentReasoningItemId == null)
        {
            _currentReasoningItemId = Guid.NewGuid().ToString();
            var item = new ThreadItem
            {
                Id = _currentReasoningItemId,
                Type = ThreadItemType.Reasoning,
                SummaryText = "",
                ItemStatus = "in_progress"
            };
            _items.Add(item);

            Notify("item/started", new
            {
                threadId = _threadId,
                turnId = _turnId,
                item
            });
        }

        _reasoningSummaryText += text;
        Notify("item/reasoning/summaryTextDelta", new
        {
            threadId = _threadId,
            turnId = _turnId,
            itemId = _currentReasoningItemId,
            summaryTextDelta = text
        });
    }

    private void HandleToolStart(BackendEvent evt)
    {
        var toolCallId = evt.ToolCallId ?? Guid.NewGuid().ToString();
        var toolName = evt.ToolName ?? "unknown";

        // Skip collab tools if not enabled
        if (!_collabEnabled && IsCollabTool(toolName))
            return;

        // Finalize any open agent message
        FinalizeAgentMessage();

        var classification = ToolItems.ClassifyToolName(toolName);
        var itemId = Guid.NewGuid().ToString();
        _activeTools[toolCallId] = itemId;
        _toolOutputCumulative[toolCallId] = "";

        ThreadItem item;
        if (classification == "commandExecution")
        {
            var command = InferCommand(evt.Input);
            item = new ThreadItem
            {
                Id = itemId,
                Type = ThreadItemType.CommandExecution,
                Command = command,
                Output = "",
                ItemStatus = "in_progress"
            };
        }
        else if (classification == "fileChange")
        {
            var filePath = InferFilePath(evt.Input);
            item = new ThreadItem
            {
                Id = itemId,
                Type = ThreadItemType.FileChange,
                FilePath = filePath,
                ChangeKind = "update",
                ItemStatus = "in_progress"
            };
        }
        else
        {
            item = new ThreadItem
            {
                Id = itemId,
                Type = ThreadItemType.AgentMessage,
                Text = "",
                ItemStatus = "in_progress"
            };
        }

        _items.Add(item);
        Notify("item/started", new
        {
            threadId = _threadId,
            turnId = _turnId,
            item
        });
    }

    private void HandleToolUpdate(BackendEvent evt)
    {
        var toolCallId = evt.ToolCallId;
        if (toolCallId == null || !_activeTools.TryGetValue(toolCallId, out var itemId))
            return;

        var outputText = ToolOutputText(evt.Output);
        if (string.IsNullOrEmpty(outputText)) return;

        // Compute true delta from cumulative output
        var prev = _toolOutputCumulative.GetValueOrDefault(toolCallId, "");
        string delta;
        if (outputText.StartsWith(prev))
        {
            delta = outputText[prev.Length..];
        }
        else
        {
            delta = outputText;
        }
        _toolOutputCumulative[toolCallId] = outputText;

        if (!string.IsNullOrEmpty(delta))
        {
            Notify("item/outputDelta", new
            {
                threadId = _threadId,
                turnId = _turnId,
                itemId,
                outputDelta = delta
            });
        }
    }

    private void HandleToolEnd(BackendEvent evt)
    {
        var toolCallId = evt.ToolCallId;
        if (toolCallId == null || !_activeTools.TryGetValue(toolCallId, out var itemId))
            return;

        _activeTools.Remove(toolCallId);
        _toolOutputCumulative.Remove(toolCallId);

        Notify("item/completed", new
        {
            threadId = _threadId,
            turnId = _turnId,
            itemId
        });
    }

    private void HandleMessageEnd()
    {
        FinalizeAgentMessage();
        FinalizeReasoning();

        Notify("turn/completed", new
        {
            threadId = _threadId,
            turnId = _turnId,
            status = "completed"
        });
    }

    private void HandleError(string error)
    {
        Notify("turn/completed", new
        {
            threadId = _threadId,
            turnId = _turnId,
            status = "failed",
            error
        });
    }

    private void HandleTokenUsage(BackendTokenUsage? usage)
    {
        if (usage == null) return;

        _usage = new ThreadTokenUsage
        {
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
            CacheReadTokens = usage.CacheReadTokens,
            CacheWriteTokens = usage.CacheWriteTokens,
            ModelContextWindow = usage.ContextWindow
        };

        Notify("turn/tokenUsage", new
        {
            threadId = _threadId,
            turnId = _turnId,
            usage = _usage
        });
    }

    private void FinalizeAgentMessage()
    {
        if (_currentAgentMessageItemId == null) return;

        // Update item text
        var idx = _items.FindIndex(i => i.Id == _currentAgentMessageItemId);
        if (idx >= 0)
        {
            _items[idx] = _items[idx] with { Text = _agentMessageText, ItemStatus = "completed" };
        }

        Notify("item/completed", new
        {
            threadId = _threadId,
            turnId = _turnId,
            itemId = _currentAgentMessageItemId
        });

        _currentAgentMessageItemId = null;
        _agentMessageText = "";
    }

    private void FinalizeReasoning()
    {
        if (_currentReasoningItemId == null) return;

        var idx = _items.FindIndex(i => i.Id == _currentReasoningItemId);
        if (idx >= 0)
        {
            _items[idx] = _items[idx] with { SummaryText = _reasoningSummaryText, ItemStatus = "completed" };
        }

        Notify("item/completed", new
        {
            threadId = _threadId,
            turnId = _turnId,
            itemId = _currentReasoningItemId
        });

        _currentReasoningItemId = null;
        _reasoningSummaryText = "";
    }

    private void Notify(string method, object @params)
    {
        _notificationSink(new TurnNotification { Method = method, Params = @params });
    }

    private static bool IsCollabTool(string name) =>
        name is "spawn_agent" or "send_input" or "wait_agent" or "close_agent" or "resume_agent";

    private static string InferCommand(JsonElement? input)
    {
        if (input == null) return "";
        if (input.Value.TryGetProperty("command", out var cmd))
            return cmd.GetString() ?? "";
        if (input.Value.TryGetProperty("cmd", out var c))
            return c.GetString() ?? "";
        return "";
    }

    private static string InferFilePath(JsonElement? input)
    {
        if (input == null) return "";
        if (input.Value.TryGetProperty("file_path", out var fp))
            return fp.GetString() ?? "";
        if (input.Value.TryGetProperty("path", out var p))
            return p.GetString() ?? "";
        if (input.Value.TryGetProperty("filePath", out var fp2))
            return fp2.GetString() ?? "";
        return "";
    }

    private static string ToolOutputText(JsonElement? output)
    {
        if (output == null) return "";
        if (output.Value.ValueKind == JsonValueKind.String)
            return output.Value.GetString() ?? "";
        if (output.Value.TryGetProperty("text", out var t))
            return t.GetString() ?? "";
        if (output.Value.TryGetProperty("output", out var o))
            return o.GetString() ?? "";
        return output.Value.ToString();
    }
}
