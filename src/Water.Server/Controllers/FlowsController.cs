using Microsoft.AspNetCore.Mvc;
using Water.Core;
using Water.Observability;
using Water.Server.Models;

namespace Water.Server.Controllers;

/// <summary>
/// MVC Controller for discovering and executing Water flows.
/// </summary>
[ApiController]
[Route("[controller]")]
public class FlowsController : ControllerBase
{
    private readonly FlowRegistry _registry;

    public FlowsController(FlowRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public IActionResult ListFlows()
    {
        var flows = _registry.GetAll().Select(f => new FlowSummaryDto
        {
            Id = f.Id,
            Description = f.Description,
            Tasks = ExtractTaskInfo(f)
        });

        return Ok(new { flows });
    }

    [HttpGet("{flowId}")]
    public IActionResult GetFlowDetails(string flowId)
    {
        var flow = _registry.Get(flowId);
        if (flow == null)
            return NotFound(new { error = $"Flow '{flowId}' not found" });

        return Ok(new FlowDetailDto
        {
            Id = flow.Id,
            Description = flow.Description,
            Metadata = flow.Metadata,
            Tasks = ExtractTaskInfo(flow)
        });
    }

    [HttpPost("{flowId}/run")]
    public async Task<IActionResult> RunFlow(string flowId, [FromBody] RunFlowRequest request)
    {
        var flow = _registry.Get(flowId);
        if (flow == null)
            return NotFound(new { error = $"Flow '{flowId}' not found" });

        try
        {
            var startTime = DateTime.UtcNow;
            var result = await flow.RunAsync(request.InputData);
            var executionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return Ok(new RunFlowResponse
            {
                FlowId = flowId,
                Status = "success",
                Result = result,
                ExecutionTimeMs = Math.Round(executionTimeMs, 4),
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, flow_id = flowId });
        }
    }

    private static List<TaskInfoDto> ExtractTaskInfo(Flow flow)
    {
        var taskInfos = new List<TaskInfoDto>();

        foreach (var node in flow.ExecutionGraph)
        {
            switch (node.Type)
            {
                case NodeType.Sequential when node.Task != null:
                    taskInfos.Add(new TaskInfoDto
                    {
                        Id = node.Task.Id,
                        Description = node.Task.Description,
                        Type = "sequential"
                    });
                    break;

                case NodeType.Parallel when node.Tasks != null:
                    foreach (var task in node.Tasks)
                        taskInfos.Add(new TaskInfoDto
                        {
                            Id = task.Id,
                            Description = task.Description,
                            Type = "parallel"
                        });
                    break;

                case NodeType.Branch when node.Branches != null:
                    foreach (var branch in node.Branches)
                        taskInfos.Add(new TaskInfoDto
                        {
                            Id = branch.Task.Id,
                            Description = branch.Task.Description,
                            Type = "branch"
                        });
                    break;

                case NodeType.Loop when node.Task != null:
                    taskInfos.Add(new TaskInfoDto
                    {
                        Id = node.Task.Id,
                        Description = node.Task.Description,
                        Type = "loop"
                    });
                    break;
            }
        }

        return taskInfos;
    }
}
