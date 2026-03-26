using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using ClaudeAgentTeamsUI.Hubs;
using ClaudeAgentTeamsUI.Models;
using ClaudeAgentTeamsUI.Services;

namespace ClaudeAgentTeamsUI.Controllers;

public class ReviewController : Controller
{
    private readonly ReviewService _reviewService;
    private readonly TeamService _teamService;
    private readonly TaskService _taskService;
    private readonly IHubContext<TeamHub> _hubContext;

    public ReviewController(ReviewService reviewService, TeamService teamService,
        TaskService taskService, IHubContext<TeamHub> hubContext)
    {
        _reviewService = reviewService;
        _teamService = teamService;
        _taskService = taskService;
        _hubContext = hubContext;
    }

    public async Task<IActionResult> Index(string teamId)
    {
        var reviews = await _reviewService.GetByTeamAsync(teamId);
        ViewBag.TeamId = teamId;
        var team = await _teamService.GetByIdAsync(teamId);
        ViewBag.TeamName = team?.Name ?? "Unknown";
        return View(reviews);
    }

    public async Task<IActionResult> Detail(string id)
    {
        var review = await _reviewService.GetByIdAsync(id);
        if (review == null) return NotFound();

        var team = await _teamService.GetByIdAsync(review.TeamId);
        var task = await _taskService.GetByIdAsync(review.TaskId);

        var vm = new ReviewViewModel
        {
            Review = review,
            TeamName = team?.Name ?? "Unknown",
            TaskTitle = task?.Title ?? "Unknown"
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> DecideHunk(string reviewId, string filePath, int hunkId,
        HunkDecision decision)
    {
        var review = await _reviewService.DecideHunkAsync(reviewId, filePath, hunkId, decision);
        if (review == null) return NotFound();
        return RedirectToAction(nameof(Detail), new { id = reviewId });
    }

    [HttpPost]
    public async Task<IActionResult> ApproveAll(string id)
    {
        var review = await _reviewService.ApproveAllAsync(id);
        if (review == null) return NotFound();
        await _hubContext.Clients.Group($"team-{review.TeamId}")
            .SendAsync(TeamHub.Events.ReviewCreated, review);
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RejectAll(string id)
    {
        var review = await _reviewService.RejectAllAsync(id);
        if (review == null) return NotFound();
        return RedirectToAction(nameof(Detail), new { id });
    }
}
