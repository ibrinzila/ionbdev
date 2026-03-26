namespace ClawTeam.Web.Transport;

/// <summary>
/// Transport interface for delivering and fetching raw message bytes
/// between agent inboxes. Mirrors the Python Transport ABC.
/// </summary>
public interface ITransport : IDisposable
{
    /// <summary>Deliver message bytes to a recipient's inbox.</summary>
    void Deliver(string recipient, byte[] data);

    /// <summary>Fetch opaque message bytes from a transport-specific inbox.</summary>
    List<byte[]> Fetch(string agentName, int limit = 10, bool consume = true);

    /// <summary>Return the number of pending messages.</summary>
    int Count(string agentName);

    /// <summary>List all known recipient names (for broadcast).</summary>
    List<string> ListRecipients();
}
