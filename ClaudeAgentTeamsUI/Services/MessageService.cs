using System.Collections.Concurrent;
using ClaudeAgentTeamsUI.Data;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Services;

public class MessageService
{
    private readonly JsonDataStore _store;
    private readonly ILogger<MessageService> _logger;
    private ConcurrentDictionary<string, InboxMessage> _messages = new();
    private ConcurrentDictionary<string, CrossTeamMessage> _crossTeamMessages = new();
    private bool _loaded;

    public MessageService(JsonDataStore store, ILogger<MessageService> logger)
    {
        _store = store;
        _logger = logger;
    }

    private async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var messages = await _store.LoadAsync<List<InboxMessage>>("messages.json");
        _messages = new ConcurrentDictionary<string, InboxMessage>(messages.ToDictionary(m => m.Id));
        var crossTeam = await _store.LoadAsync<List<CrossTeamMessage>>("cross_team_messages.json");
        _crossTeamMessages = new ConcurrentDictionary<string, CrossTeamMessage>(crossTeam.ToDictionary(m => m.Id));
        _loaded = true;
    }

    private async Task PersistAsync()
    {
        await _store.SaveAsync("messages.json", _messages.Values.ToList());
        await _store.SaveAsync("cross_team_messages.json", _crossTeamMessages.Values.ToList());
    }

    public async Task<List<InboxMessage>> GetByTeamAsync(string teamId)
    {
        await EnsureLoadedAsync();
        return _messages.Values
            .Where(m => m.TeamId == teamId)
            .OrderByDescending(m => m.Timestamp)
            .ToList();
    }

    public async Task<List<InboxMessage>> GetByMemberAsync(string teamId, string memberName)
    {
        await EnsureLoadedAsync();
        return _messages.Values
            .Where(m => m.TeamId == teamId && (m.ToMember == memberName || m.FromMember == memberName))
            .OrderByDescending(m => m.Timestamp)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(string teamId)
    {
        await EnsureLoadedAsync();
        return _messages.Values.Count(m => m.TeamId == teamId && !m.IsRead);
    }

    public async Task<InboxMessage> SendAsync(string teamId, string from, string to, string content,
        string? taskId = null, MessageSource source = MessageSource.Direct)
    {
        await EnsureLoadedAsync();
        var msg = new InboxMessage
        {
            TeamId = teamId,
            FromMember = from,
            ToMember = to,
            Content = content,
            Source = source,
            TaskId = taskId
        };
        _messages[msg.Id] = msg;
        await PersistAsync();
        _logger.LogInformation("Message sent from {From} to {To} in team {Team}", from, to, teamId);
        return msg;
    }

    public async Task<bool> MarkReadAsync(string messageId)
    {
        await EnsureLoadedAsync();
        if (!_messages.TryGetValue(messageId, out var msg)) return false;
        msg.IsRead = true;
        await PersistAsync();
        return true;
    }

    public async Task<int> MarkAllReadAsync(string teamId)
    {
        await EnsureLoadedAsync();
        int count = 0;
        foreach (var msg in _messages.Values.Where(m => m.TeamId == teamId && !m.IsRead))
        {
            msg.IsRead = true;
            count++;
        }
        if (count > 0) await PersistAsync();
        return count;
    }

    public async Task<List<CrossTeamMessage>> GetCrossTeamMessagesAsync(string teamId)
    {
        await EnsureLoadedAsync();
        return _crossTeamMessages.Values
            .Where(m => m.FromTeam == teamId || m.ToTeam == teamId)
            .OrderByDescending(m => m.Timestamp)
            .ToList();
    }

    public async Task<CrossTeamMessage> SendCrossTeamAsync(string fromTeam, string toTeam,
        string fromMember, string content)
    {
        await EnsureLoadedAsync();
        var msg = new CrossTeamMessage
        {
            FromTeam = fromTeam,
            ToTeam = toTeam,
            FromMember = fromMember,
            Content = content,
            ConversationId = $"{fromTeam}-{toTeam}"
        };
        _crossTeamMessages[msg.Id] = msg;
        await PersistAsync();
        return msg;
    }
}
