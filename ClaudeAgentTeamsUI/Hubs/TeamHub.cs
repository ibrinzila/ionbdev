using Microsoft.AspNetCore.SignalR;
using ClaudeAgentTeamsUI.Models;

namespace ClaudeAgentTeamsUI.Hubs;

public class TeamHub : Hub
{
    public async Task JoinTeamGroup(string teamId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"team-{teamId}");
    }

    public async Task LeaveTeamGroup(string teamId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"team-{teamId}");
    }

    public async Task JoinDashboard()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "dashboard");
    }

    // Server-to-client event methods (called from services)
    public static class Events
    {
        public const string TeamUpdated = "TeamUpdated";
        public const string TaskUpdated = "TaskUpdated";
        public const string TaskMoved = "TaskMoved";
        public const string MessageReceived = "MessageReceived";
        public const string NotificationReceived = "NotificationReceived";
        public const string MemberStatusChanged = "MemberStatusChanged";
        public const string ProvisioningProgress = "ProvisioningProgress";
        public const string ToolApprovalRequested = "ToolApprovalRequested";
        public const string ReviewCreated = "ReviewCreated";
        public const string ScheduleRunCompleted = "ScheduleRunCompleted";
    }
}
