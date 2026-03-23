using System.Text.Json;
using Codapter.Core.Models;
using Codapter.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Codapter.Web.Controllers;

/// <summary>
/// HTTP endpoint for JSON-RPC requests (alternative to WebSocket/SignalR).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class RpcController : ControllerBase
{
    private readonly AppServerConnection _appServer;
    private readonly ILogger<RpcController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public RpcController(AppServerConnection appServer, ILogger<RpcController> logger)
    {
        _appServer = appServer;
        _logger = logger;
    }

    /// <summary>
    /// Handle a JSON-RPC request via HTTP POST.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleRpc([FromBody] JsonElement body)
    {
        // Reject requests with an Origin header (security measure from TS version)
        if (Request.Headers.ContainsKey("Origin"))
        {
            return StatusCode(403, new { error = "Origin header not allowed" });
        }

        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(body, JsonOptions);
            if (request == null)
            {
                return BadRequest(JsonRpcHelpers.Failure(
                    default, JsonRpcErrorCodes.InvalidRequest, "Invalid request"));
            }

            var result = await _appServer.HandleRequestAsync(
                request.Method, request.Params, HttpContext.RequestAborted);

            return Ok(JsonRpcHelpers.Success(request.Id, result));
        }
        catch (JsonRpcException ex)
        {
            var id = GetRequestId(body);
            return Ok(JsonRpcHelpers.Failure(id, ex.Code, ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RPC request");
            var id = GetRequestId(body);
            return Ok(JsonRpcHelpers.Failure(id, JsonRpcErrorCodes.InternalError, ex.Message));
        }
    }

    /// <summary>
    /// Batch JSON-RPC requests.
    /// </summary>
    [HttpPost("batch")]
    public async Task<IActionResult> HandleBatch([FromBody] JsonElement body)
    {
        if (body.ValueKind != JsonValueKind.Array)
        {
            return BadRequest(new { error = "Expected array of requests" });
        }

        var responses = new List<object>();

        foreach (var element in body.EnumerateArray())
        {
            try
            {
                var request = JsonSerializer.Deserialize<JsonRpcRequest>(element, JsonOptions);
                if (request == null)
                {
                    responses.Add(JsonRpcHelpers.Failure(
                        default, JsonRpcErrorCodes.InvalidRequest, "Invalid request"));
                    continue;
                }

                var result = await _appServer.HandleRequestAsync(
                    request.Method, request.Params, HttpContext.RequestAborted);

                responses.Add(JsonRpcHelpers.Success(request.Id, result));
            }
            catch (JsonRpcException ex)
            {
                var id = GetRequestId(element);
                responses.Add(JsonRpcHelpers.Failure(id, ex.Code, ex.Message));
            }
            catch (Exception ex)
            {
                var id = GetRequestId(element);
                responses.Add(JsonRpcHelpers.Failure(id, JsonRpcErrorCodes.InternalError, ex.Message));
            }
        }

        return Ok(responses);
    }

    private static JsonRpcId GetRequestId(JsonElement body)
    {
        if (body.TryGetProperty("id", out var idElement))
        {
            return idElement.ValueKind switch
            {
                JsonValueKind.String => new JsonRpcId(idElement.GetString()!),
                JsonValueKind.Number => new JsonRpcId(idElement.GetInt64()),
                _ => default
            };
        }
        return default;
    }
}
