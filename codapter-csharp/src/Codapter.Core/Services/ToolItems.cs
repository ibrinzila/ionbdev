namespace Codapter.Core.Services;

/// <summary>
/// Tool classification and file change synthesis.
/// Port of packages/core/src/tool-items.ts
/// </summary>
public static class ToolItems
{
    private static readonly HashSet<string> CommandTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "bash", "shell", "terminal", "exec", "execute", "run",
        "command", "cmd", "sh", "zsh", "powershell"
    };

    private static readonly HashSet<string> FileTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "edit", "write", "patch", "create_file", "update_file",
        "file_edit", "file_write", "file_patch", "replace",
        "insert", "delete_file", "rename_file", "move_file"
    };

    /// <summary>
    /// Classifies a tool name into commandExecution, fileChange, or agentMessage.
    /// </summary>
    public static string ClassifyToolName(string toolName)
    {
        // Exact match first
        if (CommandTools.Contains(toolName))
            return "commandExecution";
        if (FileTools.Contains(toolName))
            return "fileChange";

        // Token-based matching
        var tokens = Tokenize(toolName);
        foreach (var token in tokens)
        {
            if (CommandTools.Contains(token))
                return "commandExecution";
            if (FileTools.Contains(token))
                return "fileChange";
        }

        return "agentMessage";
    }

    /// <summary>
    /// Tokenizes a tool name by splitting on separators and case transitions.
    /// </summary>
    private static List<string> Tokenize(string name)
    {
        var tokens = new List<string>();
        var current = "";

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c is '_' or '-' or '.' or ' ')
            {
                if (current.Length > 0)
                {
                    tokens.Add(current);
                    current = "";
                }
                continue;
            }

            // CamelCase split
            if (char.IsUpper(c) && current.Length > 0 && char.IsLower(current[^1]))
            {
                tokens.Add(current);
                current = "";
            }

            current += char.ToLowerInvariant(c);
        }

        if (current.Length > 0)
            tokens.Add(current);

        return tokens;
    }

    /// <summary>
    /// Synthesizes file change data from tool inputs/outputs.
    /// </summary>
    public static FileChangeInfo? SynthesizeFileChange(
        string toolName,
        string? inputJson,
        string? outputJson,
        string? workingDirectory = null)
    {
        if (ClassifyToolName(toolName) != "fileChange")
            return null;

        var filePath = "";
        var changeKind = "update";
        var diff = "";

        // Attempt to extract from input
        if (!string.IsNullOrEmpty(inputJson))
        {
            try
            {
                var input = System.Text.Json.JsonDocument.Parse(inputJson).RootElement;
                filePath = ExtractFilePath(input);

                if (input.TryGetProperty("old_text", out var oldText) &&
                    input.TryGetProperty("new_text", out var newText))
                {
                    diff = GenerateUnifiedDiff(
                        filePath,
                        oldText.GetString() ?? "",
                        newText.GetString() ?? "");
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        if (!string.IsNullOrEmpty(workingDirectory) && !Path.IsPathRooted(filePath))
        {
            filePath = Path.Combine(workingDirectory, filePath);
        }

        return new FileChangeInfo
        {
            FilePath = filePath,
            ChangeKind = changeKind,
            Diff = diff
        };
    }

    private static string ExtractFilePath(System.Text.Json.JsonElement input)
    {
        if (input.TryGetProperty("file_path", out var fp))
            return fp.GetString() ?? "";
        if (input.TryGetProperty("path", out var p))
            return p.GetString() ?? "";
        if (input.TryGetProperty("filePath", out var fp2))
            return fp2.GetString() ?? "";
        return "";
    }

    /// <summary>
    /// Generates a unified diff from old and new text.
    /// </summary>
    public static string GenerateUnifiedDiff(string filePath, string oldText, string newText)
    {
        var oldLines = oldText.Split('\n');
        var newLines = newText.Split('\n');

        var result = new List<string>
        {
            $"--- a/{filePath}",
            $"+++ b/{filePath}"
        };

        // Simple diff: show removed and added lines
        var maxLines = Math.Max(oldLines.Length, newLines.Length);
        var hunkStart = 1;
        var changes = new List<string>();

        for (var i = 0; i < maxLines; i++)
        {
            var oldLine = i < oldLines.Length ? oldLines[i] : null;
            var newLine = i < newLines.Length ? newLines[i] : null;

            if (oldLine == newLine)
            {
                changes.Add($" {oldLine}");
            }
            else
            {
                if (oldLine != null)
                    changes.Add($"-{oldLine}");
                if (newLine != null)
                    changes.Add($"+{newLine}");
            }
        }

        result.Add($"@@ -{hunkStart},{oldLines.Length} +{hunkStart},{newLines.Length} @@");
        result.AddRange(changes);

        return string.Join("\n", result);
    }
}

public record FileChangeInfo
{
    public string FilePath { get; init; } = "";
    public string ChangeKind { get; init; } = "update";
    public string Diff { get; init; } = "";
}
