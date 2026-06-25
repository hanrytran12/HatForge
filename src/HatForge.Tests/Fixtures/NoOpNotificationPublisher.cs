using HatForge.Application.Interfaces;

namespace HatForge.Tests.Fixtures;

public class NoOpNotificationPublisher : INotificationPublisher
{
    public Task NotifyWorkSubmittedAsync(int batchId, int workshopId, object payload) => Task.CompletedTask;
    public Task NotifyWorkApprovedAsync(int batchId, int staffId, object payload) => Task.CompletedTask;
    public Task NotifyWorkRejectedAsync(int batchId, int staffId, object payload) => Task.CompletedTask;
    public Task NotifyTransferRequestedAsync(int leadId, object payload) => Task.CompletedTask;
    public Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, object payload) => Task.CompletedTask;
    public Task NotifyBatchCompletedAsync(int batchId, int? leadId, object payload) => Task.CompletedTask;
    public Task NotifyBatchAssignedToLeadAsync(int leadId, object payload) => Task.CompletedTask;
    public Task NotifyBatchPlannedAsync(int workshopId, object payload) => Task.CompletedTask;
    public Task NotifyMaterialDeliveryConfirmedAsync(int batchId, int workshopId, object payload) => Task.CompletedTask;
    public Task NotifyWorkCanBeginAsync(int toWorkshopId, object payload) => Task.CompletedTask;
    public Task NotifyFinalReviewRequestedAsync(int leadId, object payload) => Task.CompletedTask;
    public Task NotifyGateQCReviewRequestedAsync(object payload) => Task.CompletedTask;
    public Task NotifyMaterialShortfallAsync(int leadId, int batchId, int workshopId, object payload) => Task.CompletedTask;
    public Task NotifyMaterialRequestApprovedAsync(int batchId, int workshopId, object payload) => Task.CompletedTask;
    public Task NotifyMaterialRequestFulfilledAsync(int leadId, int batchId, int workshopId, object payload) => Task.CompletedTask;
    public Task NotifyAdHocMaterialRequestAsync(int leadId, int batchId, int workshopId, object payload) => Task.CompletedTask;
}
