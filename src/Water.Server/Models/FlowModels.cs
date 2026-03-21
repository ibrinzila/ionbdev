namespace Water.Server.Models;

public class RunFlowRequest
{
    public Dictionary<string, object?> InputData { get; set; } = new();
}

public class RunFlowResponse
{
    public string FlowId { get; set; } = "";
    public string Status { get; set; } = "";
    public Dictionary<string, object?> Result { get; set; } = new();
    public double ExecutionTimeMs { get; set; }
    public DateTime Timestamp { get; set; }
}

public class FlowSummaryDto
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public List<TaskInfoDto> Tasks { get; set; } = new();
}

public class FlowDetailDto
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public Dictionary<string, object?> Metadata { get; set; } = new();
    public List<TaskInfoDto> Tasks { get; set; } = new();
}

public class TaskInfoDto
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "";
}
