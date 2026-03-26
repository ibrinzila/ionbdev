using ExpectMvc.Models;

namespace ExpectMvc.Services;

/// <summary>
/// Scans git changes — mirrors the "Scan changes" stage of the Expect pipeline.
/// Supports unstaged, branch, and combined change targets.
/// </summary>
public interface IGitService
{
    Task<GitDiff> ScanChangesAsync(string repoPath, TestTarget target, string baseBranch = "main");
}
