using Water.Core;

namespace Water.Agents;

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Policy for requiring approval on task execution.
/// </summary>
public class ApprovalPolicy
{
    public RiskLevel MinRiskLevel { get; set; } = RiskLevel.High;
    public List<string> ApproverIds { get; set; } = new();
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Represents a request for approval.
/// </summary>
public class ApprovalRequest
{
    public string Id { get; set; } = $"approval_{Guid.NewGuid().ToString("N")[..8]}";
    public string TaskId { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string Description { get; set; }
    public Dictionary<string, object?> Data { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool? Approved { get; set; }
    public string? ApprovedBy { get; set; }

    public ApprovalRequest(string taskId, RiskLevel riskLevel, string description, Dictionary<string, object?> data)
    {
        TaskId = taskId;
        RiskLevel = riskLevel;
        Description = description;
        Data = data;
    }
}

/// <summary>
/// Raised when an approval request is denied.
/// </summary>
public class ApprovalDeniedException : WaterException
{
    public ApprovalRequest Request { get; }
    public ApprovalDeniedException(ApprovalRequest request) : base($"Approval denied for task {request.TaskId}")
    {
        Request = request;
    }
}

/// <summary>
/// Gate that manages approval requests. Uses a callback to resolve approvals.
/// </summary>
public class ApprovalGate
{
    private readonly ApprovalPolicy _policy;
    private readonly Func<ApprovalRequest, Task<bool>>? _approvalHandler;
    private readonly List<ApprovalRequest> _pendingRequests = new();

    public ApprovalGate(ApprovalPolicy? policy = null, Func<ApprovalRequest, Task<bool>>? approvalHandler = null)
    {
        _policy = policy ?? new ApprovalPolicy();
        _approvalHandler = approvalHandler;
    }

    public async Task<ApprovalRequest> RequestApprovalAsync(string taskId, RiskLevel riskLevel, string description, Dictionary<string, object?> data)
    {
        if (riskLevel < _policy.MinRiskLevel)
            return new ApprovalRequest(taskId, riskLevel, description, data) { Approved = true };

        var request = new ApprovalRequest(taskId, riskLevel, description, data);
        _pendingRequests.Add(request);

        if (_approvalHandler != null)
        {
            request.Approved = await _approvalHandler(request);
            if (request.Approved != true)
                throw new ApprovalDeniedException(request);
        }

        return request;
    }

    public IReadOnlyList<ApprovalRequest> PendingRequests => _pendingRequests.AsReadOnly();
}

/// <summary>
/// Factory for creating approval-gated tasks.
/// </summary>
public static class ApprovalTaskFactory
{
    public static WaterTask Create(
        WaterTask innerTask,
        ApprovalGate gate,
        RiskLevel riskLevel = RiskLevel.High,
        string? id = null,
        string? description = null)
    {
        return new WaterTask(
            execute: async (parameters, context) =>
            {
                var inputData = parameters.TryGetValue("input_data", out var inp) && inp is Dictionary<string, object?> dict
                    ? dict : parameters;

                await gate.RequestApprovalAsync(
                    innerTask.Id, riskLevel,
                    description ?? innerTask.Description,
                    inputData);

                return await innerTask.Execute(parameters, context);
            },
            id: id ?? $"approval_{innerTask.Id}",
            description: description ?? $"Approval gate for {innerTask.Description}");
    }
}
