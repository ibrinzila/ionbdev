namespace ExpectMvc.Models;

/// <summary>
/// Represents scanned git changes used as input for test plan generation.
/// </summary>
public class GitDiff
{
    public string Repository { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string BaseBranch { get; set; } = "main";
    public string RawDiff { get; set; } = string.Empty;
    public List<FileChange> Files { get; set; } = new();
}

public class FileChange
{
    public string Path { get; set; } = string.Empty;
    public ChangeType Type { get; set; }
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string Patch { get; set; } = string.Empty;
}

public enum ChangeType
{
    Added,
    Modified,
    Deleted,
    Renamed
}
