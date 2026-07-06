using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class LeadTaskDelegationService : ILeadTaskDelegationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;

    public LeadTaskDelegationService(IUnitOfWork unitOfWork, INotificationPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<LeadTaskDelegationDto> CreateAsync(CreateLeadTaskDelegationDto dto, int leadId)
    {
        var lead = await _unitOfWork.Users.GetByIdAsync(leadId)
            ?? throw new NotFoundException("Lead not found");
        if (lead.Role != UserRole.Lead)
            throw new ForbiddenException("Only Lead can create delegation requests");

        var transportQc = await _unitOfWork.Users.GetByIdAsync(dto.AssignedTransportQcId)
            ?? throw new NotFoundException("QC Transport user not found");
        if (transportQc.Role != UserRole.QCTransport)
            throw new BusinessRuleException("Assigned user must be QC Transport");

        if (!string.IsNullOrWhiteSpace(dto.Reason) && dto.Reason.Length > 500)
            throw new BusinessRuleException("Reason must be 500 characters or fewer");

        var request = dto.Type switch
        {
            LeadTaskDelegationType.MaterialDelivery => await CreateMaterialDeliveryDelegationAsync(dto, leadId),
            LeadTaskDelegationType.TransferApproval => await CreateTransferApprovalDelegationAsync(dto, leadId),
            _ => throw new BusinessRuleException("Unsupported delegation type")
        };

        await _unitOfWork.LeadTaskDelegationRequests.AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        var result = await MapToDtoAsync(request.Id);
        await _notifications.NotifyLeadTaskDelegationRequestedAsync(new
        {
            DelegationId = result.Id,
            result.BatchId,
            result.BatchNumber,
            result.TypeName,
            result.RequestedByLeadId,
            result.RequestedByLeadName,
            result.AssignedTransportQcId,
            result.AssignedTransportQcName,
            result.Reason
        });

        return result;
    }

    public async Task<IReadOnlyList<LeadTaskDelegationDto>> GetPendingForAdminAsync(int adminId)
    {
        var admin = await _unitOfWork.Users.GetByIdAsync(adminId)
            ?? throw new NotFoundException("Admin not found");
        if (admin.Role != UserRole.Admin)
            throw new ForbiddenException("Only Admin can view pending delegation requests");

        var requests = await _unitOfWork.LeadTaskDelegationRequests.FindAsync(
            x => x.Status == LeadTaskDelegationStatus.PendingAdminApproval,
            Includes);

        return requests
            .OrderBy(x => x.CreatedAt)
            .Select(MapToDtoValue)
            .ToList();
    }

    public async Task<IReadOnlyList<LeadTaskDelegationDto>> GetRequestedByLeadAsync(int leadId)
    {
        var lead = await _unitOfWork.Users.GetByIdAsync(leadId)
            ?? throw new NotFoundException("Lead not found");
        if (lead.Role != UserRole.Lead)
            throw new ForbiddenException("Only Lead can view their delegation requests");

        var requests = await _unitOfWork.LeadTaskDelegationRequests.FindAsync(
            x => x.RequestedByLeadId == leadId,
            Includes);

        return requests
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDtoValue)
            .ToList();
    }

    public async Task<IReadOnlyList<LeadTaskDelegationDto>> GetAssignedToTransportQcAsync(int transportQcId)
    {
        var transportQc = await _unitOfWork.Users.GetByIdAsync(transportQcId)
            ?? throw new NotFoundException("QC Transport user not found");
        if (transportQc.Role != UserRole.QCTransport)
            throw new ForbiddenException("Only QC Transport can view assigned delegation requests");

        var requests = await _unitOfWork.LeadTaskDelegationRequests.FindAsync(
            x => x.AssignedTransportQcId == transportQcId
              && (x.Status == LeadTaskDelegationStatus.Approved
                  || x.Status == LeadTaskDelegationStatus.Completed),
            Includes);

        return requests
            .OrderByDescending(x => x.CreatedAt)
            .Select(MapToDtoValue)
            .ToList();
    }

    public async Task<LeadTaskDelegationDto> ApproveAsync(int delegationId, int adminId, ReviewLeadTaskDelegationDto dto)
    {
        var request = await ReviewAsync(
            delegationId,
            adminId,
            dto,
            LeadTaskDelegationStatus.Approved);

        var result = await MapToDtoAsync(request.Id);
        await _notifications.NotifyLeadTaskDelegationApprovedAsync(request.AssignedTransportQcId, new
        {
            DelegationId = result.Id,
            result.BatchId,
            result.BatchNumber,
            result.TypeName,
            result.MaterialDeliveryId,
            result.TransferRequestId,
            result.AdminNotes
        });

        return result;
    }

    public async Task<LeadTaskDelegationDto> RejectAsync(int delegationId, int adminId, ReviewLeadTaskDelegationDto dto)
    {
        var request = await ReviewAsync(
            delegationId,
            adminId,
            dto,
            LeadTaskDelegationStatus.Rejected);

        var result = await MapToDtoAsync(request.Id);
        await _notifications.NotifyLeadTaskDelegationRejectedAsync(request.RequestedByLeadId, new
        {
            DelegationId = result.Id,
            result.BatchId,
            result.BatchNumber,
            result.TypeName,
            result.AdminNotes
        });

        return result;
    }

    public async Task<LeadTaskDelegationDto> MarkMaterialDeliveredAsync(int delegationId, int transportQcId)
    {
        var request = await GetExecutableRequestAsync(
            delegationId,
            transportQcId,
            LeadTaskDelegationType.MaterialDelivery);

        var delivery = await _unitOfWork.MaterialDeliveries.GetByIdAsync(request.MaterialDeliveryId!.Value)
            ?? throw new NotFoundException("Material delivery not found");

        if (delivery.IsConfirmed || delivery.Status == MaterialDeliveryStatus.Received)
            throw new BusinessRuleException("Material delivery has already been received by workshop QC");
        if (delivery.Status == MaterialDeliveryStatus.Delivered)
            throw new BusinessRuleException("Material delivery has already been marked as delivered");

        delivery.Status = MaterialDeliveryStatus.Delivered;
        delivery.DeliveredDate = DateTime.UtcNow;
        _unitOfWork.MaterialDeliveries.Update(delivery);

        CompleteRequest(request, transportQcId);
        await _unitOfWork.SaveChangesAsync();

        var result = await MapToDtoAsync(request.Id);
        await _notifications.NotifyLeadTaskDelegationCompletedAsync(request.RequestedByLeadId, new
        {
            DelegationId = result.Id,
            result.BatchId,
            result.BatchNumber,
            result.TypeName,
            result.MaterialDeliveryId
        });

        return result;
    }

    public async Task<LeadTaskDelegationDto> ApproveDelegatedTransferAsync(int delegationId, int transportQcId)
    {
        var request = await GetExecutableRequestAsync(
            delegationId,
            transportQcId,
            LeadTaskDelegationType.TransferApproval);

        var transfer = await _unitOfWork.TransferRequests.GetByIdAsync(request.TransferRequestId!.Value)
            ?? throw new NotFoundException("Transfer request not found");

        if (transfer.Status != TransferStatus.Pending)
            throw new BusinessRuleException("Transfer is not pending");

        transfer.Status = TransferStatus.Approved;
        transfer.ApprovedByLeadId = request.RequestedByLeadId;
        transfer.ApprovedAt = DateTime.UtcNow;
        _unitOfWork.TransferRequests.Update(transfer);

        CompleteRequest(request, transportQcId);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyTransferApprovedAsync(transfer.BatchId, transfer.ToWorkshopId,
            new
            {
                TransferId = transfer.Id,
                transfer.BatchId,
                transfer.ToWorkshopId,
                DelegationId = request.Id,
                ApprovedByTransportQcId = transportQcId
            });

        var result = await MapToDtoAsync(request.Id);
        await _notifications.NotifyLeadTaskDelegationCompletedAsync(request.RequestedByLeadId, new
        {
            DelegationId = result.Id,
            result.BatchId,
            result.BatchNumber,
            result.TypeName,
            result.TransferRequestId
        });

        return result;
    }

    private async Task<LeadTaskDelegationRequest> CreateMaterialDeliveryDelegationAsync(
        CreateLeadTaskDelegationDto dto,
        int leadId)
    {
        var delivery = await _unitOfWork.MaterialDeliveries.FirstOrDefaultAsync(
            x => x.Id == dto.TaskId,
            new[] { "Batch", "Workshop" })
            ?? throw new NotFoundException("Material delivery not found");

        if (delivery.Batch?.AssignedToLeadId != leadId)
            throw new ForbiddenException("Only the assigned lead can delegate this material delivery");

        if (delivery.IsConfirmed || delivery.Status == MaterialDeliveryStatus.Received)
            throw new BusinessRuleException("Material delivery has already been received");
        if (delivery.Status == MaterialDeliveryStatus.Delivered)
            throw new BusinessRuleException("Material delivery has already been delivered");

        await EnsureNoActiveDelegationAsync(
            LeadTaskDelegationType.MaterialDelivery,
            delivery.Id,
            "There is already an active delegation for this material delivery");

        return new LeadTaskDelegationRequest
        {
            BatchId = delivery.BatchId,
            MaterialDeliveryId = delivery.Id,
            Type = LeadTaskDelegationType.MaterialDelivery,
            RequestedByLeadId = leadId,
            AssignedTransportQcId = dto.AssignedTransportQcId,
            Reason = dto.Reason
        };
    }

    private async Task<LeadTaskDelegationRequest> CreateTransferApprovalDelegationAsync(
        CreateLeadTaskDelegationDto dto,
        int leadId)
    {
        var transfer = await _unitOfWork.TransferRequests.FirstOrDefaultAsync(
            x => x.Id == dto.TaskId,
            new[] { "Batch", "FromWorkshop", "ToWorkshop" })
            ?? throw new NotFoundException("Transfer request not found");

        if (transfer.Batch?.AssignedToLeadId != leadId)
            throw new ForbiddenException("Only the assigned lead can delegate this transfer approval");

        if (transfer.Status != TransferStatus.Pending)
            throw new BusinessRuleException("Transfer is not pending");

        await EnsureNoActiveDelegationAsync(
            LeadTaskDelegationType.TransferApproval,
            transfer.Id,
            "There is already an active delegation for this transfer");

        return new LeadTaskDelegationRequest
        {
            BatchId = transfer.BatchId,
            TransferRequestId = transfer.Id,
            Type = LeadTaskDelegationType.TransferApproval,
            RequestedByLeadId = leadId,
            AssignedTransportQcId = dto.AssignedTransportQcId,
            Reason = dto.Reason
        };
    }

    private async Task EnsureNoActiveDelegationAsync(
        LeadTaskDelegationType type,
        int taskId,
        string message)
    {
        var existing = type == LeadTaskDelegationType.MaterialDelivery
            ? await _unitOfWork.LeadTaskDelegationRequests.FirstOrDefaultAsync(x =>
                x.Type == type
                && x.MaterialDeliveryId == taskId
                && (x.Status == LeadTaskDelegationStatus.PendingAdminApproval
                    || x.Status == LeadTaskDelegationStatus.Approved))
            : await _unitOfWork.LeadTaskDelegationRequests.FirstOrDefaultAsync(x =>
                x.Type == type
                && x.TransferRequestId == taskId
                && (x.Status == LeadTaskDelegationStatus.PendingAdminApproval
                    || x.Status == LeadTaskDelegationStatus.Approved));

        if (existing != null)
            throw new BusinessRuleException(message);
    }

    private async Task<LeadTaskDelegationRequest> ReviewAsync(
        int delegationId,
        int adminId,
        ReviewLeadTaskDelegationDto dto,
        LeadTaskDelegationStatus nextStatus)
    {
        var admin = await _unitOfWork.Users.GetByIdAsync(adminId)
            ?? throw new NotFoundException("Admin not found");
        if (admin.Role != UserRole.Admin)
            throw new ForbiddenException("Only Admin can review delegation requests");

        if (!string.IsNullOrWhiteSpace(dto.AdminNotes) && dto.AdminNotes.Length > 500)
            throw new BusinessRuleException("Admin notes must be 500 characters or fewer");

        var request = await _unitOfWork.LeadTaskDelegationRequests.GetByIdAsync(delegationId)
            ?? throw new NotFoundException("Delegation request not found");

        if (request.Status != LeadTaskDelegationStatus.PendingAdminApproval)
            throw new BusinessRuleException("Delegation request is not pending admin approval");

        request.Status = nextStatus;
        request.ReviewedByAdminId = adminId;
        request.ReviewedAt = DateTime.UtcNow;
        request.AdminNotes = dto.AdminNotes;
        _unitOfWork.LeadTaskDelegationRequests.Update(request);
        await _unitOfWork.SaveChangesAsync();

        return request;
    }

    private async Task<LeadTaskDelegationRequest> GetExecutableRequestAsync(
        int delegationId,
        int transportQcId,
        LeadTaskDelegationType expectedType)
    {
        var transportQc = await _unitOfWork.Users.GetByIdAsync(transportQcId)
            ?? throw new NotFoundException("QC Transport user not found");
        if (transportQc.Role != UserRole.QCTransport)
            throw new ForbiddenException("Only QC Transport can execute delegated lead tasks");

        var request = await _unitOfWork.LeadTaskDelegationRequests.GetByIdAsync(delegationId)
            ?? throw new NotFoundException("Delegation request not found");

        if (request.Type != expectedType)
            throw new BusinessRuleException($"Delegation request is not for {expectedType}");
        if (request.AssignedTransportQcId != transportQcId)
            throw new ForbiddenException("You can only execute delegation requests assigned to you");
        if (request.Status != LeadTaskDelegationStatus.Approved)
            throw new BusinessRuleException("Delegation request has not been approved by Admin");

        return request;
    }

    private void CompleteRequest(LeadTaskDelegationRequest request, int transportQcId)
    {
        request.Status = LeadTaskDelegationStatus.Completed;
        request.CompletedByTransportQcId = transportQcId;
        request.CompletedAt = DateTime.UtcNow;
        _unitOfWork.LeadTaskDelegationRequests.Update(request);
    }

    private async Task<LeadTaskDelegationDto> MapToDtoAsync(int id)
    {
        var request = await _unitOfWork.LeadTaskDelegationRequests.FirstOrDefaultAsync(
            x => x.Id == id,
            Includes)
            ?? throw new NotFoundException("Delegation request not found");

        return MapToDtoValue(request);
    }

    private static LeadTaskDelegationDto MapToDtoValue(LeadTaskDelegationRequest r) => new(
        r.Id,
        r.BatchId,
        r.Batch?.BatchNumber ?? "",
        r.Type,
        r.Type.ToString(),
        r.Status,
        r.Status.ToString(),
        r.MaterialDeliveryId,
        r.TransferRequestId,
        r.RequestedByLeadId,
        r.RequestedByLead?.Name ?? "",
        r.AssignedTransportQcId,
        r.AssignedTransportQc?.Name ?? "",
        r.ReviewedByAdminId,
        r.ReviewedByAdmin?.Name,
        r.CompletedByTransportQcId,
        r.CompletedByTransportQc?.Name,
        r.Reason,
        r.AdminNotes,
        r.CreatedAt,
        r.ReviewedAt,
        r.CompletedAt);

    private static readonly string[] Includes =
    {
        "Batch",
        "RequestedByLead",
        "AssignedTransportQc",
        "ReviewedByAdmin",
        "CompletedByTransportQc"
    };
}
