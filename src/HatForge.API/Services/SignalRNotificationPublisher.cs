using HatForge.API.Hubs;
using HatForge.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace HatForge.API.Services;

public class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationPublisher(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task NotifyWorkSubmittedAsync(int batchId, int workshopId, object payload) =>
        _hub.Clients.Group($"workshop_{workshopId}").SendAsync("WorkSubmitted", payload);

    public Task NotifyWorkApprovedAsync(int batchId, int staffId, object payload) =>
        Task.WhenAll(
            _hub.Clients.Group($"user_{staffId}").SendAsync("WorkApproved", payload),
            _hub.Clients.Group($"batch_{batchId}").SendAsync("WorkApproved", payload),
            _hub.Clients.Group("leads").SendAsync("WorkApproved", payload));

    public Task NotifyWorkRejectedAsync(int batchId, int staffId, object payload) =>
        _hub.Clients.Group($"user_{staffId}").SendAsync("WorkRejected", payload);

    public Task NotifyTransferRequestedAsync(object payload) =>
        _hub.Clients.Group("leads").SendAsync("TransferRequested", payload);

    public Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, object payload) =>
        Task.WhenAll(
            _hub.Clients.Group($"workshop_{toWorkshopId}").SendAsync("TransferApproved", payload),
            _hub.Clients.Group($"batch_{batchId}").SendAsync("TransferApproved", payload));

    public Task NotifyBatchCompletedAsync(int batchId, object payload) =>
        Task.WhenAll(
            _hub.Clients.Group($"batch_{batchId}").SendAsync("BatchCompleted", payload),
            _hub.Clients.Group("admins").SendAsync("BatchCompleted", payload));

    public Task NotifyBatchCreatedAsync(object payload) =>
        _hub.Clients.Group("admins").SendAsync("BatchCreated", payload);
}
