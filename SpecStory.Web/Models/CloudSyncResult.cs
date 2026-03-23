namespace SpecStory.Web.Models;

public class CloudSyncResult
{
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Errored { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;

    public bool HasErrors => Errored > 0;
    public int Total => Created + Updated + Skipped + Errored;
}
