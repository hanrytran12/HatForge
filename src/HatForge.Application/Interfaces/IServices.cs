using HatForge.Application.DTOs;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;

namespace HatForge.Application.Interfaces;

public interface IBatchService
{
    Task<BatchDto> CreateBatchAsync(CreateBatchDto dto);
    Task<BatchDto> PlanBatchAsync(int batchId, PlanBatchDto dto, int leadId);
    Task<BatchDto?> GetBatchByIdAsync(int id);
    Task<IReadOnlyList<BatchListDto>> GetAllBatchesAsync();
    Task<IReadOnlyList<BatchListDto>> GetBatchesByLeadAsync(int leadId);
    Task<IReadOnlyList<BatchListDto>> GetBatchesByStatusAsync(BatchStatus status);
    Task<BatchDto> MarkWorkshopCompletedAsync(int batchId, int workshopId, int actorId);
    Task<BatchDto> LeadApproveFinalAsync(int batchId, int leadId);
    Task<BatchDto> GateConfirmAsync(int batchId, int qcGateId);
    Task<BatchFinalSummaryDto> GetFinalSummaryAsync(int batchId);
}

public interface IHatModelService
{
    Task<HatModelDto> CreateAsync(CreateHatModelDto dto);
    Task<IReadOnlyList<HatModelDto>> GetAllAsync();
    Task<HatModelDto> UpdateAsync(int id, UpdateHatModelDto dto);
    Task DeleteAsync(int id);
}

public interface IWorkService
{
    Task<WorkDto> SubmitWorkAsync(SubmitWorkDto dto, int staffId);
    Task<WorkDto> ApproveWorkAsync(ApproveWorkDto dto, int qcId);
    Task<WorkDto> RejectWorkAsync(RejectWorkDto dto, int qcId);
    Task<IReadOnlyList<WorkDto>> GetWorksByBatchAsync(int batchId);
    Task<IReadOnlyList<WorkDto>> GetWorksByBatchAndWorkshopAsync(int batchId, int workshopId);
}

public interface ITransferService
{
    Task<CreateTransferResultDto> CreateTransferRequestAsync(CreateTransferDto dto, int qcId);
    Task<TransferRequestDto> ApproveTransferAsync(ApproveTransferDto dto, int leadId);
    Task<TransferRequestDto> ConfirmReceiptAsync(ConfirmReceiptDto dto, int qcId);
    Task<IReadOnlyList<TransferRequestDto>> GetPendingTransfersAsync();
    Task<IReadOnlyList<TransferRequestDto>> GetAwaitingReceiptByWorkshopAsync(int workshopId);
}

public interface IMaterialDeliveryService
{
    Task<IReadOnlyList<MaterialDeliveryDto>> GetPendingByWorkshopAsync(int workshopId);
    Task<MaterialDeliveryDto> ConfirmDeliveryAsync(ConfirmMaterialDeliveryDto dto, int qcId);
}

public interface IMaterialRequestService
{
    Task<MaterialRequestDto?> CreateRequestFromShortfallAsync(int deliveryId, int qcId);
    Task<MaterialRequestDto> CreateAdHocRequestAsync(CreateAdHocMaterialRequestDto dto, int qcId);
    Task<IReadOnlyList<MaterialRequestDto>> GetPendingForLeadAsync(int leadId);
    Task<IReadOnlyList<MaterialRequestDto>> GetByBatchAsync(int batchId);
    Task<MaterialRequestDto> ApproveAsync(int requestId, int leadId);
    Task<MaterialRequestDto> ConfirmAsync(ConfirmMaterialRequestDto dto, int qcId);
}

public interface ILeadTaskDelegationService
{
    Task<LeadTaskDelegationDto> CreateAsync(CreateLeadTaskDelegationDto dto, int leadId);
    Task<IReadOnlyList<LeadTaskDelegationDto>> GetRequestedByLeadAsync(int leadId);
    Task<IReadOnlyList<LeadTaskDelegationDto>> GetPendingForAdminAsync(int adminId);
    Task<IReadOnlyList<LeadTaskDelegationDto>> GetAssignedToTransportQcAsync(int transportQcId);
    Task<LeadTaskDelegationDto> ApproveAsync(int delegationId, int adminId, ReviewLeadTaskDelegationDto dto);
    Task<LeadTaskDelegationDto> RejectAsync(int delegationId, int adminId, ReviewLeadTaskDelegationDto dto);
    Task<LeadTaskDelegationDto> MarkMaterialDeliveredAsync(int delegationId, int transportQcId);
    Task<LeadTaskDelegationDto> ApproveDelegatedTransferAsync(int delegationId, int transportQcId);
    Task<LeadTaskDelegationDto> ApproveDelegatedFinalReviewAsync(int delegationId, int transportQcId);
    Task<LeadTaskDelegationDto> MarkMaterialRequestDeliveredAsync(int delegationId, int transportQcId);
}

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
}

public interface IUserService
{
    Task<UserDto> CreateAsync(RegisterDto dto);
    Task<IReadOnlyList<UserDto>> GetAllAsync();
    Task DeleteStaffAsync(int id);
}

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetMyNotificationsAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task MarkAsReadAsync(int notificationId, int userId);
    Task MarkAllAsReadAsync(int userId);
}
