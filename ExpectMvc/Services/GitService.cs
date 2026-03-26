using System.Diagnostics;
using ExpectMvc.Models;

namespace ExpectMvc.Services;

public class GitService : IGitService
{
    private readonly ILogger<GitService> _logger;

    public GitService(ILogger<GitService> logger)
    {
        _logger = logger;
    }

    public async Task<GitDiff> ScanChangesAsync(string repoPath, TestTarget target, string baseBranch = "main")
    {
        _logger.LogInformation("Scanning {Target} in {Repo}", target, repoPath);

        var diffArgs = target switch
        {
            TestTarget.Unstaged => "diff",
            TestTarget.Branch => $"diff {baseBranch}...HEAD",
            TestTarget.Changes => "diff HEAD",
            _ => "diff HEAD"
        };

        var rawDiff = await RunGitAsync(repoPath, diffArgs);
        var branch = (await RunGitAsync(repoPath, "rev-parse --abbrev-ref HEAD")).Trim();
        var files = ParseDiffStat(await RunGitAsync(repoPath, $"{diffArgs} --stat"));
        var patches = await GetFilePatchesAsync(repoPath, diffArgs, files);

        return new GitDiff
        {
            Repository = repoPath,
            Branch = branch,
            BaseBranch = baseBranch,
            RawDiff = rawDiff,
            Files = patches
        };
    }

    private static List<FileChange> ParseDiffStat(string stat)
    {
        var files = new List<FileChange>();
        foreach (var line in stat.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("---") || trimmed.StartsWith("+++") ||
                !trimmed.Contains('|'))
                continue;

            var parts = trimmed.Split('|');
            if (parts.Length < 2) continue;

            var path = parts[0].Trim();
            var changePart = parts[1].Trim();

            var additions = changePart.Count(c => c == '+');
            var deletions = changePart.Count(c => c == '-');

            var type = changePart.Contains("new") ? ChangeType.Added
                : changePart.Contains("delete") ? ChangeType.Deleted
                : changePart.Contains("rename") ? ChangeType.Renamed
                : ChangeType.Modified;

            files.Add(new FileChange
            {
                Path = path,
                Type = type,
                Additions = additions,
                Deletions = deletions
            });
        }
        return files;
    }

    private static async Task<List<FileChange>> GetFilePatchesAsync(
        string repoPath, string diffArgs, List<FileChange> files)
    {
        if (files.Count == 0) return files;

        var rawDiff = await RunGitAsync(repoPath, diffArgs);
        var patches = rawDiff.Split("diff --git ", StringSplitOptions.RemoveEmptyEntries);

        foreach (var patch in patches)
        {
            var firstLine = patch.Split('\n').FirstOrDefault() ?? "";
            var matchingFile = files.FirstOrDefault(f => firstLine.Contains(f.Path));
            if (matchingFile != null)
            {
                matchingFile.Patch = "diff --git " + patch;
            }
        }

        return files;
    }

    private static async Task<string> RunGitAsync(string workingDir, string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}
