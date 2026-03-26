namespace ExpectMvc.Models;

/// <summary>
/// Accessibility snapshot of a page, with ref-based element identifiers.
/// Mirrors @expect/browser's snapshot() output.
/// </summary>
public class SnapshotResult
{
    public string Tree { get; set; } = string.Empty;
    public Dictionary<string, RefEntry> Refs { get; set; } = new();
}

public class RefEntry
{
    public string Role { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? Nth { get; set; }
}
