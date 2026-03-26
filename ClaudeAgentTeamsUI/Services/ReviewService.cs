using System.Collections.Concurrent;
using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class ReviewService
{
    private readonly JsonDataStore _store;
    private readonly ILogger<ReviewService> _logger;
    private ConcurrentDictionary<string, ChangeReview> _reviews = new();
    private bool _loaded;

    public ReviewService(JsonDataStore store, ILogger<ReviewService> logger)
    {
        _store = store;
        _logger = logger;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var reviews = await _store.LoadAsync<List<ChangeReview>>("reviews.json");
        _reviews = new ConcurrentDictionary<string, ChangeReview>(reviews.ToDictionary(r => r.Id));
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        await _store.SaveAsync("reviews.json", _reviews.Values.ToList());
    }

    public async Task<List<ChangeReview>> GetByTeamAsync(string teamId)
    {
        await EnsureLoadedAsync();
        return _reviews.Values
            .Where(r => r.TeamId == teamId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task<ChangeReview?> GetByIdAsync(string id)
    {
        await EnsureLoadedAsync();
        return _reviews.GetValueOrDefault(id);
    }

    public async Task<ChangeReview> CreateAsync(string teamId, string taskId, string? agentId,
        List<FileChange> files)
    {
        await EnsureLoadedAsync();
        var review = new ChangeReview
        {
            TeamId = teamId,
            TaskId = taskId,
            AgentId = agentId,
            Files = files
        };
        _reviews[review.Id] = review;
        await PersistAsync();
        _logger.LogInformation("Created review {ReviewId} for task {TaskId}", review.Id, taskId);
        return review;
    }

    public async Task<ChangeReview?> DecideHunkAsync(string reviewId, string filePath, int hunkId,
        HunkDecision decision)
    {
        await EnsureLoadedAsync();
        if (!_reviews.TryGetValue(reviewId, out var review)) return null;

        var file = review.Files.FirstOrDefault(f => f.FilePath == filePath);
        var hunk = file?.Hunks.FirstOrDefault(h => h.Id == hunkId);
        if (hunk != null)
        {
            hunk.Decision = decision;
            UpdateOverallState(review);
            await PersistAsync();
        }
        return review;
    }

    public async Task<ChangeReview?> ApproveAllAsync(string reviewId)
    {
        await EnsureLoadedAsync();
        if (!_reviews.TryGetValue(reviewId, out var review)) return null;

        foreach (var file in review.Files)
            foreach (var hunk in file.Hunks)
                hunk.Decision = HunkDecision.Accepted;

        review.State = ReviewDecisionState.Approved;
        review.DecidedAt = DateTime.UtcNow;
        await PersistAsync();
        return review;
    }

    public async Task<ChangeReview?> RejectAllAsync(string reviewId)
    {
        await EnsureLoadedAsync();
        if (!_reviews.TryGetValue(reviewId, out var review)) return null;

        foreach (var file in review.Files)
            foreach (var hunk in file.Hunks)
                hunk.Decision = HunkDecision.Rejected;

        review.State = ReviewDecisionState.Rejected;
        review.DecidedAt = DateTime.UtcNow;
        await PersistAsync();
        return review;
    }

    private void UpdateOverallState(ChangeReview review)
    {
        var allHunks = review.Files.SelectMany(f => f.Hunks).ToList();
        if (allHunks.All(h => h.Decision == HunkDecision.Accepted))
            review.State = ReviewDecisionState.Approved;
        else if (allHunks.All(h => h.Decision == HunkDecision.Rejected))
            review.State = ReviewDecisionState.Rejected;
        else if (allHunks.Any(h => h.Decision != HunkDecision.Pending))
            review.State = ReviewDecisionState.PartiallyApproved;
        else
            review.State = ReviewDecisionState.Pending;

        if (review.State != ReviewDecisionState.Pending)
            review.DecidedAt = DateTime.UtcNow;
    }
}
