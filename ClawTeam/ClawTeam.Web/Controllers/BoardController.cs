using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ClawTeam.Web.Services;

namespace ClawTeam.Web.Controllers;

/// <summary>Dashboard board with kanban view and SSE real-time updates.</summary>
public class BoardController : Controller
{
    private readonly BoardCollector _collector;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BoardController(BoardCollector collector)
    {
        _collector = collector;
    }

    // GET /Board
    public IActionResult Index()
    {
        var teams = _collector.CollectOverview();
        return View(teams);
    }

    // GET /Board/Team/{name}
    public IActionResult Team(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest("Team name is required");

        try
        {
            var detail = _collector.CollectTeam(id);
            return View(detail);
        }
        catch (InvalidOperationException)
        {
            return NotFound($"Team '{id}' not found");
        }
    }

    // ── API Endpoints (JSON) ─────────────────────────────────────────

    // GET /Board/ApiOverview
    [HttpGet]
    public IActionResult ApiOverview()
    {
        var data = _collector.CollectOverview();
        return Json(data);
    }

    // GET /Board/ApiTeam/{name}
    [HttpGet]
    public IActionResult ApiTeam(string id)
    {
        if (string.IsNullOrEmpty(id)) return BadRequest();

        try
        {
            var data = _collector.CollectTeam(id);
            return Json(data);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    // GET /Board/Events/{teamName} — Server-Sent Events endpoint
    [HttpGet]
    public async Task Events(string id, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(id))
        {
            Response.StatusCode = 400;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                object data;
                try
                {
                    data = _collector.CollectTeam(id);
                }
                catch (Exception ex)
                {
                    data = new { error = ex.Message };
                }

                var json = JsonSerializer.Serialize(data, JsonOpts);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);

                await Task.Delay(2000, ct);
            }
        }
        catch (OperationCanceledException) { }
    }
}
