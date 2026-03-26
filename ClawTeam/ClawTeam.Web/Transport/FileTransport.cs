using System.Text;

namespace ClawTeam.Web.Transport;

/// <summary>
/// File-system-based message transport. Messages are stored as JSON files
/// in {DataDir}/teams/{Team}/inboxes/{Agent}/msg-{ts}-{uid}.json.
/// Atomic writes via temp file + rename.
/// </summary>
public class FileTransport : ITransport
{
    private readonly string _dataDir;
    private readonly string _teamName;
    private readonly object _lock = new();

    public FileTransport(string dataDir, string teamName)
    {
        _dataDir = dataDir;
        _teamName = teamName;
    }

    private string InboxDir(string agentName)
    {
        var dir = Path.Combine(_dataDir, "teams", _teamName, "inboxes", agentName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private string DeadLetterDir(string agentName)
    {
        var dir = Path.Combine(_dataDir, "teams", _teamName, "inboxes", agentName, "dead_letters");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public void Deliver(string recipient, byte[] data)
    {
        var dir = InboxDir(recipient);
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var uid = Guid.NewGuid().ToString("N")[..8];
        var filename = $"msg-{ts}-{uid}.json";
        var finalPath = Path.Combine(dir, filename);
        var tempPath = finalPath + ".tmp";

        // Atomic write: write to temp then rename
        File.WriteAllBytes(tempPath, data);
        File.Move(tempPath, finalPath);
    }

    public List<byte[]> Fetch(string agentName, int limit = 10, bool consume = true)
    {
        var dir = InboxDir(agentName);
        var results = new List<byte[]>();

        lock (_lock)
        {
            var files = Directory.GetFiles(dir, "msg-*.json")
                .OrderBy(f => f)
                .Take(limit)
                .ToList();

            foreach (var file in files)
            {
                try
                {
                    var data = File.ReadAllBytes(file);
                    results.Add(data);

                    if (consume)
                    {
                        var consumed = file + ".consumed";
                        try
                        {
                            File.Move(file, consumed);
                        }
                        catch
                        {
                            // Best-effort consumption
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Move to dead letter queue
                    try
                    {
                        var dlDir = DeadLetterDir(agentName);
                        var dlPath = Path.Combine(dlDir, Path.GetFileName(file));
                        File.Move(file, dlPath);

                        var metaPath = dlPath + ".meta.json";
                        var meta = $"{{\"error\":\"{ex.Message}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}";
                        File.WriteAllText(metaPath, meta);
                    }
                    catch { }
                }
            }
        }

        return results;
    }

    public int Count(string agentName)
    {
        var dir = InboxDir(agentName);
        if (!Directory.Exists(dir)) return 0;
        return Directory.GetFiles(dir, "msg-*.json").Length;
    }

    public List<string> ListRecipients()
    {
        var inboxRoot = Path.Combine(_dataDir, "teams", _teamName, "inboxes");
        if (!Directory.Exists(inboxRoot)) return new List<string>();

        return Directory.GetDirectories(inboxRoot)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => n!)
            .ToList();
    }

    public void Dispose()
    {
        // No resources to release for file-based transport
    }
}
