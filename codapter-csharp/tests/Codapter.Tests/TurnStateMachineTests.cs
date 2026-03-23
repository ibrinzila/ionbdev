using Codapter.Core.Models;
using Codapter.Core.Services;
using Xunit;

namespace Codapter.Tests;

public class TurnStateMachineTests
{
    private readonly List<TurnNotification> _notifications = new();
    private readonly TurnStateMachine _tsm;

    public TurnStateMachineTests()
    {
        _tsm = new TurnStateMachine("thread-1", "turn-1", n => _notifications.Add(n));
    }

    [Fact]
    public void TextDelta_CreatesAgentMessageItem()
    {
        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.TextDelta,
            SessionId = "s1",
            Text = "Hello"
        });

        Assert.Equal(2, _notifications.Count);
        Assert.Equal("item/started", _notifications[0].Method);
        Assert.Equal("item/agentMessage/delta", _notifications[1].Method);
        Assert.Single(_tsm.Items);
        Assert.Equal(ThreadItemType.AgentMessage, _tsm.Items[0].Type);
    }

    [Fact]
    public void ThinkingDelta_CreatesReasoningItem()
    {
        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.ThinkingDelta,
            SessionId = "s1",
            Text = "Let me think..."
        });

        Assert.Equal("item/started", _notifications[0].Method);
        Assert.Equal("item/reasoning/summaryTextDelta", _notifications[1].Method);
        Assert.Equal(ThreadItemType.Reasoning, _tsm.Items[0].Type);
    }

    [Fact]
    public void ToolLifecycle_EmitsStartUpdateEnd()
    {
        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.ToolStart,
            SessionId = "s1",
            ToolCallId = "tc1",
            ToolName = "bash"
        });

        Assert.Contains(_notifications, n => n.Method == "item/started");

        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.ToolEnd,
            SessionId = "s1",
            ToolCallId = "tc1"
        });

        Assert.Contains(_notifications, n => n.Method == "item/completed");
    }

    [Fact]
    public void MessageEnd_EmitsTurnCompleted()
    {
        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.TextDelta,
            SessionId = "s1",
            Text = "Done"
        });

        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.MessageEnd,
            SessionId = "s1"
        });

        Assert.Contains(_notifications, n => n.Method == "turn/completed");
    }

    [Fact]
    public void Error_EmitsFailedTurn()
    {
        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.Error,
            SessionId = "s1",
            Error = "Something went wrong"
        });

        Assert.Contains(_notifications, n => n.Method == "turn/completed");
    }

    [Fact]
    public void TokenUsage_TracksUsage()
    {
        _tsm.HandleEvent(new BackendEvent
        {
            Type = BackendEventType.TokenUsage,
            SessionId = "s1",
            Usage = new BackendTokenUsage
            {
                InputTokens = 100,
                OutputTokens = 50,
                CacheReadTokens = 10,
                CacheWriteTokens = 5
            }
        });

        Assert.NotNull(_tsm.Usage);
        Assert.Equal(100, _tsm.Usage!.InputTokens);
        Assert.Equal(50, _tsm.Usage!.OutputTokens);
    }
}
