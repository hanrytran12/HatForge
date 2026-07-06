namespace HatForge.Application.Interfaces;

public interface INotificationPublisher
{
    Task NotifyWorkSubmittedAsync(int batchId, int workshopId, object payload);
    Task NotifyWorkApprovedAsync(int batchId, int staffId, object payload);
    Task NotifyWorkRejectedAsync(int batchId, int staffId, object payload);
    Task NotifyTransferRequestedAsync(int leadId, object payload);
    Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, object payload);
    Task NotifyBatchCompletedAsync(int batchId, int? leadId, object payload);
    Task NotifyBatchAssignedToLeadAsync(int leadId, object payload);
    Task NotifyBatchPlannedAsync(int workshopId, object payload);
    Task NotifyMaterialDeliveryConfirmedAsync(int batchId, int workshopId, object payload);
    Task NotifyWorkCanBeginAsync(int toWorkshopId, object payload);
    Task NotifyFinalReviewRequestedAsync(int leadId, object payload);
    Task NotifyGateQCReviewRequestedAsync(object payload);
    Task NotifyMaterialShortfallAsync(int leadId, int batchId, int workshopId, object payload);
    Task NotifyMaterialRequestApprovedAsync(int batchId, int workshopId, object payload);
    Task NotifyMaterialRequestFulfilledAsync(int leadId, int batchId, int workshopId, object payload);
    Task NotifyAdHocMaterialRequestAsync(int leadId, int batchId, int workshopId, object payload);
    Task NotifyMaterialLowAlertAsync(int batchId, int workshopId, object payload);
    Task NotifyLeadTaskDelegationRequestedAsync(object payload);
    Task NotifyLeadTaskDelegationApprovedAsync(int transportQcId, object payload);
    Task NotifyLeadTaskDelegationRejectedAsync(int leadId, object payload);
    Task NotifyLeadTaskDelegationCompletedAsync(int leadId, object payload);
}
