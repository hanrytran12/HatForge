using System.Text.Json;
using HatForge.API.Hubs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace HatForge.API.Services;

public class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SignalRNotificationPublisher(IHubContext<NotificationHub> hub, IUnitOfWork unitOfWork)
    {
        _hub = hub;
        _unitOfWork = unitOfWork;
    }

    public async Task NotifyWorkSubmittedAsync(int batchId, int workshopId, object payload) =>
        await _hub.Clients.Group($"workshop_{workshopId}").SendAsync("WorkSubmitted", payload);

    public async Task NotifyWorkApprovedAsync(int batchId, int staffId, object payload)
    {
        await Task.WhenAll(
            _hub.Clients.Group($"user_{staffId}").SendAsync("WorkApproved", payload),
            _hub.Clients.Group($"batch_{batchId}").SendAsync("WorkApproved", payload),
            _hub.Clients.Group("leads").SendAsync("WorkApproved", payload));

        await SaveAsync(staffId, "WorkApproved",
            "Your work was approved",
            $"Work for batch #{batchId} has been approved by QC.",
            payload);
    }

    public async Task NotifyWorkRejectedAsync(int batchId, int staffId, object payload)
    {
        await _hub.Clients.Group($"user_{staffId}").SendAsync("WorkRejected", payload);

        await SaveAsync(staffId, "WorkRejected",
            "Your work was rejected",
            $"Work for batch #{batchId} was rejected. Please review and resubmit.",
            payload);
    }

    public async Task NotifyTransferRequestedAsync(int leadId, object payload)
    {
        await Task.WhenAll(
            _hub.Clients.Group("leads").SendAsync("TransferRequested", payload),
            _hub.Clients.Group($"user_{leadId}").SendAsync("TransferRequested", payload));

        await SaveAsync(leadId, "TransferRequested",
            "Yêu cầu duyệt chuyển lô",
            "Một xưởng yêu cầu bạn xuống duyệt lại lô hàng trước khi chuyển.",
            payload);
    }

    public async Task NotifyTransferApprovedAsync(int batchId, int toWorkshopId, object payload)
    {
        await Task.WhenAll(
            _hub.Clients.Group($"workshop_{toWorkshopId}").SendAsync("TransferApproved", payload),
            _hub.Clients.Group($"batch_{batchId}").SendAsync("TransferApproved", payload));

        var qcUsers = await _unitOfWork.Users.FindAsync(
            x => x.WorkshopId == toWorkshopId && x.Role == Domain.Enums.UserRole.QCWorkshop);

        foreach (var qc in qcUsers)
        {
            await SaveAsync(qc.Id, "TransferApproved",
                "Có lô hàng chờ bạn xác nhận nhận",
                $"Lô hàng đã được duyệt và đang chờ xưởng của bạn xác nhận đã nhận được.",
                payload);
        }
    }

    public async Task NotifyBatchCompletedAsync(int batchId, object payload) =>
        await Task.WhenAll(
            _hub.Clients.Group($"batch_{batchId}").SendAsync("BatchCompleted", payload),
            _hub.Clients.Group("admins").SendAsync("BatchCompleted", payload));

    public async Task NotifyBatchAssignedToLeadAsync(int leadId, object payload)
    {
        await Task.WhenAll(
            _hub.Clients.Group($"user_{leadId}").SendAsync("BatchAssigned", payload),
            _hub.Clients.Group("admins").SendAsync("BatchCreated", payload));

        await SaveAsync(leadId, "BatchAssigned",
            "New batch assigned to you",
            "Admin has assigned a new batch for you to plan.",
            payload);
    }

    public async Task NotifyBatchPlannedAsync(int workshopId, object payload)
    {
        await _hub.Clients.Group($"workshop_{workshopId}").SendAsync("BatchPlanned", payload);

        // Persist notification for all QC users of this workshop
        var qcUsers = await _unitOfWork.Users.FindAsync(
            x => x.WorkshopId == workshopId &&
                 (x.Role == Domain.Enums.UserRole.QCWorkshop));

        foreach (var qc in qcUsers)
        {
            await SaveAsync(qc.Id, "BatchPlanned",
                "Your workshop has been assigned to a batch",
                $"Workshop has been scheduled in a batch production plan.",
                payload);
        }
    }

    public async Task NotifyMaterialDeliveryConfirmedAsync(int batchId, int workshopId, object payload)
    {
        await Task.WhenAll(
            _hub.Clients.Group($"batch_{batchId}").SendAsync("MaterialConfirmed", payload),
            _hub.Clients.Group("leads").SendAsync("MaterialConfirmed", payload),
            _hub.Clients.Group($"workshop_{workshopId}").SendAsync("MaterialConfirmed", payload));

        // Persist notification for all Staff of this workshop so they know they can begin work
        var staffUsers = await _unitOfWork.Users.FindAsync(
            x => x.WorkshopId == workshopId && x.Role == Domain.Enums.UserRole.Staff);

        foreach (var staff in staffUsers)
        {
            await SaveAsync(staff.Id, "MaterialConfirmed",
                "Materials have arrived — you can begin work",
                "Materials for your workshop have been confirmed. You may now submit work.",
                payload);
        }
    }

    public async Task NotifyWorkCanBeginAsync(int toWorkshopId, object payload)
    {
        await _hub.Clients.Group($"workshop_{toWorkshopId}").SendAsync("WorkCanBegin", payload);

        var staffUsers = await _unitOfWork.Users.FindAsync(
            x => x.WorkshopId == toWorkshopId && x.Role == Domain.Enums.UserRole.Staff);

        foreach (var staff in staffUsers)
        {
            await SaveAsync(staff.Id, "WorkCanBegin",
                "Your workshop can now begin work",
                "Materials/batch have arrived. You can now submit work for this batch.",
                payload);
        }
    }

    private async Task SaveAsync(int userId, string type, string title, string message, object payload)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Payload = JsonSerializer.Serialize(payload, JsonOptions)
        };

        await _unitOfWork.Notifications.AddAsync(notification);
        await _unitOfWork.SaveChangesAsync();
    }
}
