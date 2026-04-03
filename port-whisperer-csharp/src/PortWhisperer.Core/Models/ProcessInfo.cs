namespace PortWhisperer.Core.Models;

public record ProcessInfo
{
    public int Pid { get; init; }
    public string ProcessName { get; init; } = "";
    public string Command { get; init; } = "";
    public string? Description { get; init; }
    public double Cpu { get; init; }
    public string? Memory { get; init; }
    public string? Cwd { get; init; }
    public string? ProjectName { get; init; }
    public string? Framework { get; init; }
    public string? Uptime { get; init; }
}
