using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class MaterialDeliveryService : IMaterialDeliveryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;
    private readonly IMaterialRequestService _materialRequestService;

    public MaterialDeliveryService(
        IUnitOfWork unitOfWork,
        INotificationPublisher notifications,
        IMaterialRequestService materialRequestService)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _materialRequestService = materialRequestService;
    }

    public async Task<IReadOnlyList<MaterialDeliveryDto>> GetPendingByWorkshopAsync(int workshopId)
    {
        var deliveries = await _unitOfWork.MaterialDeliveries.FindAsync(
            x => x.WorkshopId == workshopId && !x.IsConfirmed,
            new[] { "Workshop", "Batch", "Items" });

        return deliveries
            .OrderBy(x => x.ScheduledDate)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<MaterialDeliveryDto> ConfirmDeliveryAsync(ConfirmMaterialDeliveryDto dto, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");

        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can confirm material receipt");

        var delivery = await _unitOfWork.MaterialDeliveries.FirstOrDefaultAsync(
            x => x.Id == dto.DeliveryId,
            new[] { "Workshop", "Batch", "Items" })
            ?? throw new NotFoundException("Delivery not found");

        if (delivery.IsConfirmed)
            throw new BusinessRuleException("Delivery has already been confirmed");

        if (qc.WorkshopId != delivery.WorkshopId)
            throw new ForbiddenException("You can only confirm deliveries for your own workshop");

        await EnsureTransportDelegationIsDeliveredAsync(delivery);

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BusinessRuleException("At least one item must be confirmed");

        var deliveryItemIds = delivery.Items.Select(x => x.Id).ToHashSet();
        var confirmedItemIds = dto.Items.Select(x => x.ItemId).ToList();
        if (confirmedItemIds.Distinct().Count() != confirmedItemIds.Count)
            throw new BusinessRuleException("Each material item can only be confirmed once");

        var missingItemIds = deliveryItemIds.Except(confirmedItemIds).ToList();
        if (missingItemIds.Count > 0)
            throw new BusinessRuleException("All material delivery items must be confirmed");

        // Update actual quantities per item
        foreach (var confirmItem in dto.Items)
        {
            if (confirmItem.ActualQuantity < 0)
                throw new BusinessRuleException($"Actual quantity for item {confirmItem.ItemId} must be greater than or equal to 0");

            var item = delivery.Items.FirstOrDefault(x => x.Id == confirmItem.ItemId)
                ?? throw new NotFoundException($"Material item {confirmItem.ItemId} not found in this delivery");

            item.ActualQuantity = confirmItem.ActualQuantity;
            _unitOfWork.MaterialDeliveryItems.Update(item);
        }

        delivery.IsConfirmed = true;
        delivery.ConfirmedByQCId = qcId;
        delivery.ConfirmedAt = DateTime.UtcNow;
        delivery.DeliveredDate ??= DateTime.UtcNow;
        delivery.Status = MaterialDeliveryStatus.Received;
        _unitOfWork.MaterialDeliveries.Update(delivery);

        // Mark materials received on BatchWorkshop
        var bw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == delivery.BatchId && x.WorkshopId == delivery.WorkshopId);
        if (bw != null)
        {
            bw.MaterialsReceived = true;
            bw.InitialMaterialQty = delivery.Items.Sum(i => (decimal)i.ActualQuantity);
            _unitOfWork.BatchWorkshops.Update(bw);
        }

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyMaterialDeliveryConfirmedAsync(
            delivery.BatchId, delivery.WorkshopId, new
            {
                DeliveryId = delivery.Id,
                delivery.BatchId,
                BatchNumber = delivery.Batch?.BatchNumber,
                delivery.WorkshopId,
                WorkshopName = delivery.Workshop?.Name
            });

        await _materialRequestService.CreateRequestFromShortfallAsync(delivery.Id, qcId);

        return MapToDto(delivery);
    }

    private async Task EnsureTransportDelegationIsDeliveredAsync(Domain.Entities.MaterialDelivery delivery)
    {
        if (delivery.Status == MaterialDeliveryStatus.Delivered)
            return;

        var activeDelegation = await _unitOfWork.LeadTaskDelegationRequests.FirstOrDefaultAsync(x =>
            x.Type == LeadTaskDelegationType.MaterialDelivery
            && x.MaterialDeliveryId == delivery.Id
            && (x.Status == LeadTaskDelegationStatus.PendingAdminApproval
                || x.Status == LeadTaskDelegationStatus.Approved));

        if (activeDelegation != null)
            throw new BusinessRuleException("Material delivery is waiting for QC Transport to mark it as delivered");
    }

    private static MaterialDeliveryDto MapToDto(Domain.Entities.MaterialDelivery d) => new(
        d.Id, d.BatchId, d.WorkshopId, d.Workshop?.Name ?? "",
        d.ScheduledDate, d.DeliveredDate,
        d.IsConfirmed, d.Status.ToString(),
        d.Items.Select(i => new MaterialDeliveryItemDto(
            i.Id, i.MaterialName, i.PlannedQuantity, i.ActualQuantity)).ToList());
}
