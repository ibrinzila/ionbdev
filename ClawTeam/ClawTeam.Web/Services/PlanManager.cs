namespace ClawTeam.Web.Services;

/// <summary>
/// Plan approval workflow: agents submit plans to leaders for review.
/// Plans stored as {DataDir}/plans/{Team}/{Agent}-{PlanId}.md.
/// </summary>
public class PlanManager
{
    private readonly string _dataDir;
    private readonly string _teamName;
    private readonly TeamManager _teamManager;

    public PlanManager(string dataDir, string teamName, TeamManager teamManager)
    {
        _dataDir = dataDir;
        _teamName = teamName;
        _teamManager = teamManager;
    }

    private string PlansDir()
    {
        var dir = Path.Combine(_dataDir, "plans", _teamName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string PlanPath(string agentName, string planId) =>
        Path.Combine(PlansDir(), $"{agentName}-{planId}.md");

    public string SubmitPlan(string agentName, string planContent, string? summary = null)
    {
        var planId = Guid.NewGuid().ToString("N")[..8];
        var path = PlanPath(agentName, planId);

        // Write plan content to disk
        File.WriteAllText(path, planContent);

        // Notify the leader
        var mailbox = new MailboxManager(_dataDir, _teamName);
        var leaderName = _teamManager.GetLeaderName(_teamName);
        if (leaderName != null)
        {
            mailbox.Send(
                from: agentName,
                to: leaderName,
                content: summary ?? $"Plan submitted: {planId}",
                type: "plan_approval_request",
                planId: planId,
                summary: summary
            );
        }

        return planId;
    }

    public void ApprovePlan(string fromAgent, string planId, string targetAgent,
        string? feedback = null)
    {
        var mailbox = new MailboxManager(_dataDir, _teamName);
        mailbox.Send(
            from: fromAgent,
            to: targetAgent,
            content: $"Plan {planId} approved. {feedback ?? ""}".Trim(),
            type: "plan_approved",
            planId: planId,
            feedback: feedback
        );
    }

    public void RejectPlan(string fromAgent, string planId, string targetAgent,
        string? feedback = null)
    {
        var mailbox = new MailboxManager(_dataDir, _teamName);
        mailbox.Send(
            from: fromAgent,
            to: targetAgent,
            content: $"Plan {planId} rejected. {feedback ?? ""}".Trim(),
            type: "plan_rejected",
            planId: planId,
            feedback: feedback
        );
    }

    public string? GetPlan(string agentName, string planId)
    {
        var path = PlanPath(agentName, planId);
        if (File.Exists(path)) return File.ReadAllText(path);

        // Legacy flat storage fallback
        var legacyPath = Path.Combine(_dataDir, "plans", $"{agentName}-{planId}.md");
        if (File.Exists(legacyPath)) return File.ReadAllText(legacyPath);

        return null;
    }

    public List<(string AgentName, string PlanId, string Filename)> ListPlans()
    {
        var dir = PlansDir();
        if (!Directory.Exists(dir)) return new();

        return Directory.GetFiles(dir, "*.md")
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f);
                var lastDash = name.LastIndexOf('-');
                if (lastDash < 0) return (name, "", Path.GetFileName(f));
                return (name[..lastDash], name[(lastDash + 1)..], Path.GetFileName(f));
            })
            .ToList();
    }
}
