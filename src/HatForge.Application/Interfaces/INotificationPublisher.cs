namespace HatForge.Application.Interfaces;

public interface INotificationPublisher
{
    Task NotifyWorkSubmittedAsync(int batchId, int workshopId, object payload);
    Task NotifyWorkApprovedAsync(int batchId, int staffId, object payload);
    Task NotifyWorkRejectedAsync(int batchId, int staffId, object payload);
    Task NotifyTransferRequestedAsync(object payload);
    Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, object payload);
    Task NotifyBatchCompletedAsync(int batchId, object payload);
    Task NotifyBatchAssignedToLeadAsync(int leadId, object payload);
    Task NotifyBatchPlannedAsync(int workshopId, object payload);
    Task NotifyMaterialDeliveryConfirmedAsync(int batchId, int workshopId, object payload);
}
