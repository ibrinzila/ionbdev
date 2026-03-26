using System.Diagnostics;
using System.Text.RegularExpressions;
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

        // Use --numstat for accurate addition/deletion counts
        var numstat = await RunGitAsync(repoPath, $"{diffArgs} --numstat");
        var files = ParseNumstat(numstat);

        // Also pick up untracked files for the Changes target
        if (target == TestTarget.Changes)
        {
            var untrackedOutput = await RunGitAsync(repoPath, "ls-files --others --exclude-standard");
            foreach (var line in untrackedOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var path = line.Trim();
                if (!string.IsNullOrEmpty(path) && files.All(f => f.Path != path))
                {
                    files.Add(new FileChange
                    {
                        Path = path,
                        Type = ChangeType.Added,
                        Additions = 0,
                        Deletions = 0,
                        Patch = $"(untracked new file: {path})"
                    });
                }
            }
        }

        // Use --diff-filter to detect file status (A/M/D/R)
        var nameStatus = await RunGitAsync(repoPath, $"{diffArgs} --name-status");
        ApplyFileStatuses(files, nameStatus);

        // Assign patches from the raw diff
        AssignPatches(files, rawDiff);

        return new GitDiff
        {
            Repository = repoPath,
            Branch = branch,
            BaseBranch = baseBranch,
            RawDiff = rawDiff,
            Files = files
        };
    }

    private static List<FileChange> ParseNumstat(string numstat)
    {
        var files = new List<FileChange>();
        foreach (var line in numstat.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            // Format: "additions\tdeletions\tfilepath"
            // Binary files show as: "-\t-\tfilepath"
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;

            var path = parts[2].Trim();
            int.TryParse(parts[0], out var additions);
            int.TryParse(parts[1], out var deletions);

            files.Add(new FileChange
            {
                Path = path,
                Type = ChangeType.Modified, // will be updated by --name-status
                Additions = additions,
                Deletions = deletions
            });
        }
        return files;
    }

    private static void ApplyFileStatuses(List<FileChange> files, string nameStatus)
    {
        foreach (var line in nameStatus.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var statusCode = parts[0].Trim();
            var path = parts.Length >= 3 ? parts[2].Trim() : parts[1].Trim(); // Renames have old\tnew

            var matchingFile = files.FirstOrDefault(f => f.Path == path);
            if (matchingFile == null && parts.Length >= 3)
            {
                // Try matching on old path for renames
                matchingFile = files.FirstOrDefault(f => f.Path == parts[1].Trim());
            }
            if (matchingFile == null) continue;

            matchingFile.Type = statusCode[0] switch
            {
                'A' => ChangeType.Added,
                'D' => ChangeType.Deleted,
                'R' => ChangeType.Renamed,
                _ => ChangeType.Modified
            };
        }
    }

    private static void AssignPatches(List<FileChange> files, string rawDiff)
    {
        if (string.IsNullOrEmpty(rawDiff)) return;

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
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(
                $"git {arguments} failed (exit {process.ExitCode}): {error}");
        }

        return output;
    }
}
