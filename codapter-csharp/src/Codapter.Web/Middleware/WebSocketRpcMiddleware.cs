using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Codapter.Core.Models;
using Codapter.Core.Utilities;
using Codapter.Web.Services;

namespace Codapter.Web.Middleware;

/// <summary>
/// Raw WebSocket middleware for JSON-RPC communication via NDJSON.
/// This provides the same transport as the TypeScript WS listener.
/// </summary>
public class WebSocketRpcMiddleware
{
    private readonly RequestDelegate _next;

    public WebSocketRpcMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/ws/rpc" && context.WebSockets.IsWebSocketRequest)
        {
            // Reject requests with Origin header
            if (context.Request.Headers.ContainsKey("Origin"))
            {
                context.Response.StatusCode = 403;
                return;
            }

            using var ws = await context.WebSockets.AcceptWebSocketAsync();
            var appServer = context.RequestServices.GetRequiredService<AppServerConnection>();
            var logger = context.RequestServices.GetRequiredService<ILogger<WebSocketRpcMiddleware>>();

            await HandleWebSocketAsync(ws, appServer, logger, context.RequestAborted);
        }
        else
        {
            await _next(context);
        }
    }

    private static async Task HandleWebSocketAsync(
        WebSocket ws,
        AppServerConnection appServer,
        ILogger logger,
        CancellationToken ct)
    {
        var buffer = new byte[16384];
        var messageBuffer = new StringBuilder();

        // Subscribe to notifications
        void SendNotification(JsonRpcNotification notification)
        {
            try
            {
                var json = NdJson.SerializeLine(notification);
                var bytes = Encoding.UTF8.GetBytes(json);
                _ = ws.SendAsync(bytes.AsMemory(), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                // Connection may have closed
            }
        }

        appServer.OnNotification += SendNotification;

        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var result = await ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var raw = messageBuffer.ToString();
                    messageBuffer.Clear();

                    // Process each NDJSON line
                    foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        try
                        {
                            var element = NdJson.ParseLine(line);
                            if (element == null) continue;

                            var request = JsonSerializer.Deserialize<JsonRpcRequest>(element.Value);
                            if (request == null) continue;

                            object response;
                            try
                            {
                                var responseResult = await appServer.HandleRequestAsync(
                                    request.Method, request.Params, ct);
                                response = JsonRpcHelpers.Success(request.Id, responseResult);
                            }
                            catch (JsonRpcException ex)
                            {
                                response = JsonRpcHelpers.Failure(request.Id, ex.Code, ex.Message);
                            }
                            catch (Exception ex)
                            {
                                response = JsonRpcHelpers.Failure(
                                    request.Id, JsonRpcErrorCodes.InternalError, ex.Message);
                            }

                            var responseJson = NdJson.SerializeLine(response);
                            var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                            await ws.SendAsync(responseBytes.AsMemory(), WebSocketMessageType.Text, true, ct);
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Error processing WebSocket message");
                        }
                    }
                }
            }
        }
        finally
        {
            appServer.OnNotification -= SendNotification;

            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
            }
        }
    }
}

public static class WebSocketRpcMiddlewareExtensions
{
    public static IApplicationBuilder UseWebSocketRpc(this IApplicationBuilder app)
    {
        return app.UseMiddleware<WebSocketRpcMiddleware>();
    }
}
