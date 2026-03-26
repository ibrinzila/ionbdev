namespace ClawTeam.Web.Spawn;

/// <summary>
/// Adapter for preparing native CLI commands (claude, codex, gemini, kimi, etc.)
/// with proper flags for permissions, prompts, and workspaces.
/// </summary>
public class NativeCliAdapter
{
    public PreparedCommand PrepareCommand(List<string> command, string? prompt = null,
        string? cwd = null, bool skipPermissions = false, string? agentName = null)
    {
        var normalized = new List<string>(command);
        var final = new List<string>(normalized);
        string? postLaunchPrompt = null;

        var basename = CommandBasename(command);

        if (skipPermissions)
        {
            if (IsClaudeCommand(basename))
                final.Add("--dangerously-skip-permissions");
            else if (IsCodexCommand(basename))
                final.Add("--dangerously-bypass-approvals-and-sandbox");
            else if (IsGeminiCommand(basename) || IsKimiCommand(basename) ||
                     IsQwenCommand(basename) || IsOpenCodeCommand(basename))
                final.Add("--yolo");
        }

        if (IsKimiCommand(basename))
        {
            if (cwd != null && !HasWorkspaceArg(command))
                final.AddRange(new[] { "-w", cwd });
            if (prompt != null)
                final.AddRange(new[] { "--print", "-p", prompt });
        }
        else if (IsNanobotCommand(basename))
        {
            if (cwd != null && !HasWorkspaceArg(command))
                final.AddRange(new[] { "-w", cwd });
            if (prompt != null)
                final.AddRange(new[] { "-m", prompt });
        }
        else if (prompt != null)
        {
            final.AddRange(new[] { "-p", prompt });
        }

        return new PreparedCommand
        {
            NormalizedCommand = normalized,
            FinalCommand = final,
            PostLaunchPrompt = postLaunchPrompt,
        };
    }

    private static string CommandBasename(List<string> command)
    {
        if (command.Count == 0) return "";
        return Path.GetFileName(command[0]).ToLower();
    }

    private static bool IsClaudeCommand(string basename) =>
        basename is "claude" or "claude-code";

    private static bool IsCodexCommand(string basename) =>
        basename is "codex" or "codex-cli";

    private static bool IsNanobotCommand(string basename) =>
        basename == "nanobot";

    private static bool IsGeminiCommand(string basename) =>
        basename == "gemini";

    private static bool IsKimiCommand(string basename) =>
        basename == "kimi";

    private static bool IsQwenCommand(string basename) =>
        basename is "qwen" or "qwen-code";

    private static bool IsOpenCodeCommand(string basename) =>
        basename == "opencode";

    private static bool HasWorkspaceArg(List<string> command) =>
        command.Contains("-w") || command.Contains("--workspace");
}

public class PreparedCommand
{
    public List<string> NormalizedCommand { get; set; } = new();
    public List<string> FinalCommand { get; set; } = new();
    public string? PostLaunchPrompt { get; set; }
}
