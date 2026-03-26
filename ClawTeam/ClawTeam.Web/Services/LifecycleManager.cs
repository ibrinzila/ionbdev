namespace ClawTeam.Web.Services;

/// <summary>
/// Manages agent lifecycle operations: shutdown requests/approvals, idle notifications.
/// </summary>
public class LifecycleManager
{
    private readonly string _dataDir;
    private readonly string _teamName;
    private readonly TeamManager _teamManager;

    public LifecycleManager(string dataDir, string teamName, TeamManager teamManager)
    {
        _dataDir = dataDir;
        _teamName = teamName;
        _teamManager = teamManager;
    }

    public string RequestShutdown(string fromAgent, string targetAgent, string? reason = null)
    {
        var mailbox = new MailboxManager(_dataDir, _teamName);
        var requestId = Guid.NewGuid().ToString("N")[..12];

        var leaderName = _teamManager.GetLeaderName(_teamName);
        var to = leaderName ?? targetAgent;

        mailbox.Send(
            from: fromAgent,
            to: to,
            content: $"Shutdown requested for {targetAgent}: {reason ?? "no reason given"}",
            type: "shutdown_request",
            requestId: requestId,
            reason: reason
        );

        return requestId;
    }

    public void ApproveShutdown(string fromAgent, string requestId, string targetAgent)
    {
        var mailbox = new MailboxManager(_dataDir, _teamName);
        mailbox.Send(
            from: fromAgent,
            to: targetAgent,
            content: $"Shutdown approved (request {requestId})",
            type: "shutdown_approved",
            requestId: requestId
        );
    }

    public void RejectShutdown(string fromAgent, string requestId, string targetAgent,
        string? feedback = null)
    {
        var mailbox = new MailboxManager(_dataDir, _teamName);
        mailbox.Send(
            from: fromAgent,
            to: targetAgent,
            content: $"Shutdown rejected (request {requestId}): {feedback ?? ""}",
            type: "shutdown_rejected",
            requestId: requestId,
            feedback: feedback
        );
    }

    public void SendIdle(string agentName)
    {
        var mailbox = new MailboxManager(_dataDir, _teamName);
        var leaderName = _teamManager.GetLeaderName(_teamName);
        if (leaderName == null) return;

        mailbox.Send(
            from: agentName,
            to: leaderName,
            content: $"{agentName} is idle and available for work",
            type: "idle"
        );
    }

    public void CleanupTeam(bool force = false)
    {
        _teamManager.Cleanup(_teamName, force);
    }
}
