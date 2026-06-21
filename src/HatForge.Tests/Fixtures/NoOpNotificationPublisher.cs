using HatForge.Application.Interfaces;

namespace HatForge.Tests.Fixtures;

public class NoOpNotificationPublisher : INotificationPublisher
{
    public Task NotifyWorkSubmittedAsync(int batchId, int workshopId, object payload) => Task.CompletedTask;
    public Task NotifyWorkApprovedAsync(int batchId, int staffId, object payload) => Task.CompletedTask;
    public Task NotifyWorkRejectedAsync(int batchId, int staffId, object payload) => Task.CompletedTask;
    public Task NotifyTransferRequestedAsync(object payload) => Task.CompletedTask;
    public Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, object payload) => Task.CompletedTask;
    public Task NotifyBatchCompletedAsync(int batchId, object payload) => Task.CompletedTask;
    public Task NotifyBatchCreatedAsync(object payload) => Task.CompletedTask;
}
