using Microsoft.AspNetCore.SignalR;

namespace HatForge.API.Hubs;

public class NotificationHub : Hub
{
    public async Task JoinBatch(int batchId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"batch_{batchId}");

    public async Task LeaveBatch(int batchId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"batch_{batchId}");

    public async Task JoinWorkshop(int workshopId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"workshop_{workshopId}");

    public async Task LeaveWorkshop(int workshopId) =>
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"workshop_{workshopId}");

    public async Task JoinAdmins() =>
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

    public async Task JoinLeads() =>
        await Groups.AddToGroupAsync(Context.ConnectionId, "leads");

    public async Task JoinUser(int userId) =>
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
}
