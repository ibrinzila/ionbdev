using System.Text.Json;
using Codapter.Core.Models;
using Codapter.Web.Services;
using Microsoft.AspNetCore.SignalR;

namespace Codapter.Web.Hubs;

/// <summary>
/// SignalR hub for real-time JSON-RPC communication.
/// Replaces the WebSocket transport from the TypeScript CLI.
/// </summary>
public class RpcHub : Hub
{
    private readonly AppServerConnection _appServer;
    private readonly ILogger<RpcHub> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public RpcHub(AppServerConnection appServer, ILogger<RpcHub> logger)
    {
        _appServer = appServer;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // Subscribe to notifications and forward them to this client
        _appServer.OnNotification += SendNotificationToClient;
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _appServer.OnNotification -= SendNotificationToClient;
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Handles a JSON-RPC request from the client.
    /// </summary>
    public async Task<object> RpcRequest(JsonElement message)
    {
        try
        {
            var request = JsonSerializer.Deserialize<JsonRpcRequest>(message, JsonOptions);
            if (request == null)
            {
                return JsonRpcHelpers.Failure(
                    default, JsonRpcErrorCodes.InvalidRequest, "Invalid request");
            }

            var result = await _appServer.HandleRequestAsync(
                request.Method, request.Params, Context.ConnectionAborted);

            return JsonRpcHelpers.Success(request.Id, result);
        }
        catch (JsonRpcException ex)
        {
            var id = GetRequestId(message);
            return JsonRpcHelpers.Failure(id, ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling RPC request");
            var id = GetRequestId(message);
            return JsonRpcHelpers.Failure(id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private void SendNotificationToClient(JsonRpcNotification notification)
    {
        try
        {
            Clients.Caller.SendAsync("RpcNotification", notification);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notification to client");
        }
    }

    private static JsonRpcId GetRequestId(JsonElement message)
    {
        if (message.TryGetProperty("id", out var idElement))
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
