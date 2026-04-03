namespace PortWhisperer.Core.Models;

public record PortInfo
{
    public int Port { get; init; }
    public int Pid { get; init; }
    public string ProcessName { get; init; } = "";
    public string RawName { get; init; } = "";
    public string Command { get; init; } = "";
    public string? Cwd { get; init; }
    public string? ProjectName { get; init; }
    public string? Framework { get; init; }
    public string? Uptime { get; init; }
    public DateTime? StartTime { get; init; }
    public PortStatus Status { get; init; } = PortStatus.Healthy;
    public string? Memory { get; init; }
    public string? GitBranch { get; init; }
    public List<ProcessTreeNode> ProcessTree { get; init; } = [];
}

public record ProcessTreeNode(int Pid, int ParentPid, string Name);

public enum PortStatus
{
    Healthy,
    Orphaned,
    Zombie,
    Unknown
}
