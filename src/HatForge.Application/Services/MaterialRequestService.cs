using HatForge.Application.Common;
using HatForge.Application.DTOs;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class MaterialRequestService : IMaterialRequestService
{
    private const int MaxSupplementalRounds = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;

    public MaterialRequestService(IUnitOfWork unitOfWork, INotificationPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<MaterialRequestDto?> CreateRequestFromShortfallAsync(int deliveryId, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can create material requests");

        var delivery = await _unitOfWork.MaterialDeliveries.FirstOrDefaultAsync(
            x => x.Id == deliveryId,
            new[] { "Items", "Batch.AssignedToLead", "Workshop" })
            ?? throw new NotFoundException("Delivery not found");

        var shortfalls = delivery.Items
            .Where(i => i.ActualQuantity < i.PlannedQuantity)
            .Select(i => new
            {
                i.Id,
                i.MaterialName,
                Unit = "",
                Shortfall = i.PlannedQuantity - i.ActualQuantity
            })
            .ToList();

        if (shortfalls.Count == 0)
            return null;

        var existingPending = await _unitOfWork.MaterialRequests.FirstOrDefaultAsync(
            x => x.OriginalDeliveryId == deliveryId && x.Status == MaterialRequestStatus.Pending);
        if (existingPending != null)
            return await MapToDtoAsync(existingPending.Id);

        var round = (await _unitOfWork.MaterialRequests.FindAsync(
            x => x.OriginalDeliveryId == deliveryId)).Count + 1;

        var request = new MaterialRequest
        {
            OriginalDeliveryId = deliveryId,
            BatchId = delivery.BatchId,
            WorkshopId = delivery.WorkshopId,
            Status = MaterialRequestStatus.Pending,
            CreatedByQCId = qcId,
            Round = round
        };

        await _unitOfWork.MaterialRequests.AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        foreach (var shortfall in shortfalls)
        {
            await _unitOfWork.MaterialRequestItems.AddAsync(new MaterialRequestItem
            {
                MaterialRequestId = request.Id,
                MaterialName = shortfall.MaterialName,
                Unit = shortfall.Unit,
                ShortfallQuantity = shortfall.Shortfall
            });
        }
        await _unitOfWork.SaveChangesAsync();

        if (delivery.Batch?.AssignedToLeadId is int leadId)
        {
            var workshopId = delivery.WorkshopId;
            var items = shortfalls.Select(s => new
            {
                s.MaterialName,
                ShortfallQuantity = s.Shortfall
            }).ToList();

            await _notifications.NotifyMaterialShortfallAsync(leadId, delivery.BatchId, workshopId, new
            {
                RequestId = request.Id,
                delivery.BatchId,
                workshopId,
                Round = round,
                Items = items
            });
        }

        return await MapToDtoAsync(request.Id);
    }

    public async Task<MaterialRequestDto> CreateAdHocRequestAsync(CreateAdHocMaterialRequestDto dto, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can create material requests");
        if (qc.WorkshopId != dto.WorkshopId)
            throw new ForbiddenException("You can only create material requests for your own workshop");

        var batch = await _unitOfWork.Batches.GetByIdAsync(dto.BatchId)
            ?? throw new NotFoundException("Batch not found");

        var allowedStatuses = new[]
        {
            BatchStatus.InProduction,
            BatchStatus.UnderQCReview,
            BatchStatus.ReadyForTransfer,
            BatchStatus.PendingLeadReview
        };
        if (!allowedStatuses.Contains(batch.Status))
            throw new BusinessRuleException(
                $"Cannot create ad-hoc material request when batch is in {batch.Status} status");

        var workshop = await _unitOfWork.Workshops.GetByIdAsync(dto.WorkshopId)
            ?? throw new NotFoundException("Workshop not found");

        var batchWorkshop = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == dto.WorkshopId);
        if (batchWorkshop == null)
            throw new BusinessRuleException(
                "Workshop is not part of this batch's production chain");

        var request = new MaterialRequest
        {
            OriginalDeliveryId = null,
            BatchId = dto.BatchId,
            WorkshopId = dto.WorkshopId,
            Status = MaterialRequestStatus.Pending,
            CreatedByQCId = qcId,
            Round = 1,
            IsAdHoc = true,
            Reason = dto.Reason
        };

        await _unitOfWork.MaterialRequests.AddAsync(request);
        await _unitOfWork.SaveChangesAsync();

        foreach (var item in dto.Items)
        {
            await _unitOfWork.MaterialRequestItems.AddAsync(new MaterialRequestItem
            {
                MaterialRequestId = request.Id,
                MaterialName = item.MaterialName,
                Unit = item.Unit,
                ShortfallQuantity = item.RequestedQuantity
            });
        }
        await _unitOfWork.SaveChangesAsync();

        if (batch.AssignedToLeadId is int leadId)
        {
            await _notifications.NotifyAdHocMaterialRequestAsync(
                leadId, dto.BatchId, dto.WorkshopId, new
                {
                    RequestId = request.Id,
                    BatchId = dto.BatchId,
                    WorkshopId = dto.WorkshopId,
                    WorkshopName = workshop.Name,
                    Reason = dto.Reason,
                    Round = 1,
                    Items = dto.Items.Select(i => new
                    {
                        i.MaterialName,
                        i.Unit,
                        RequestedQuantity = i.RequestedQuantity
                    }).ToList()
                });
        }

        return await MapToDtoAsync(request.Id);
    }

    public async Task<IReadOnlyList<MaterialRequestDto>> GetPendingForLeadAsync(int leadId)
    {
        var allRequests = await _unitOfWork.MaterialRequests.FindAsync(
            x => x.Status == MaterialRequestStatus.Pending,
            new[] { "Batch", "OriginalDelivery.Workshop", "Workshop", "Items" });

        var filtered = allRequests
            .Where(r => r.Batch?.AssignedToLeadId == leadId)
            .ToList();

        var results = new List<MaterialRequestDto>();
        foreach (var r in filtered)
            results.Add(await MapToDtoAsync(r.Id));
        return results;
    }

    public async Task<IReadOnlyList<MaterialRequestDto>> GetByBatchAsync(int batchId)
    {
        var requests = await _unitOfWork.MaterialRequests.FindAsync(
            x => x.BatchId == batchId,
            new[] { "Batch", "OriginalDelivery.Workshop", "Workshop", "Items" });

        var results = new List<MaterialRequestDto>();
        foreach (var r in requests)
            results.Add(await MapToDtoAsync(r.Id));
        return results;
    }

    public async Task<MaterialRequestDto> ApproveAsync(int requestId, int leadId)
    {
        var lead = await _unitOfWork.Users.GetByIdAsync(leadId)
            ?? throw new NotFoundException("Lead not found");
        if (lead.Role != UserRole.Lead)
            throw new ForbiddenException("Only Lead can approve material requests");

        var request = await _unitOfWork.MaterialRequests.GetByIdAsync(requestId)
            ?? throw new NotFoundException("Material request not found");

        if (request.Status != MaterialRequestStatus.Pending)
            throw new BusinessRuleException("Material request is not pending");

        var batch = await _unitOfWork.Batches.GetByIdAsync(request.BatchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.AssignedToLeadId != leadId)
            throw new ForbiddenException("Only the assigned lead can approve this material request");

        request.Status = MaterialRequestStatus.Approved;
        request.ApprovedByLeadId = leadId;
        request.ApprovedAt = DateTime.UtcNow;
        _unitOfWork.MaterialRequests.Update(request);
        await _unitOfWork.SaveChangesAsync();

        int notifyWorkshopId = request.WorkshopId;
        if (request.OriginalDeliveryId is int deliveryId)
        {
            var delivery = await _unitOfWork.MaterialDeliveries.GetByIdAsync(deliveryId)
                ?? throw new NotFoundException("Original delivery not found");
            notifyWorkshopId = delivery.WorkshopId;
        }

        await _notifications.NotifyMaterialRequestApprovedAsync(
            request.BatchId, notifyWorkshopId, new
            {
                RequestId = request.Id,
                BatchId = request.BatchId,
                WorkshopId = notifyWorkshopId,
                Round = request.Round
            });

        return await MapToDtoAsync(request.Id);
    }

    public async Task<MaterialRequestDto> ConfirmAsync(ConfirmMaterialRequestDto dto, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can confirm material request receipt");

        var request = await _unitOfWork.MaterialRequests.GetByIdAsync(dto.RequestId)
            ?? throw new NotFoundException("Material request not found");

        if (request.Status != MaterialRequestStatus.Approved)
            throw new BusinessRuleException("Material request must be approved by Lead before fulfillment");

        if (request.Round >= MaxSupplementalRounds + 1)
            throw new BusinessRuleException($"Maximum supplemental rounds ({MaxSupplementalRounds}) reached");

        MaterialDelivery? delivery = null;
        int targetWorkshopId;
        if (request.OriginalDeliveryId is int deliveryId)
        {
            delivery = await _unitOfWork.MaterialDeliveries.GetByIdAsync(deliveryId)
                ?? throw new NotFoundException("Original delivery not found");
            targetWorkshopId = delivery.WorkshopId;
        }
        else
        {
            targetWorkshopId = request.WorkshopId;
        }

        if (qc.WorkshopId != targetWorkshopId)
            throw new ForbiddenException("You can only confirm material requests for your own workshop");

        var items = await _unitOfWork.MaterialRequestItems.FindAsync(
            x => x.MaterialRequestId == request.Id);

        if (dto.Items == null || dto.Items.Count == 0)
            throw new BusinessRuleException("At least one item must be confirmed");

        var stillShort = new List<(MaterialRequestItem item, int shortfall)>();

        foreach (var confirmItem in dto.Items)
        {
            if (confirmItem.ActualQuantity <= 0)
                throw new BusinessRuleException($"Actual quantity for item {confirmItem.ItemId} must be greater than 0");

            var item = items.FirstOrDefault(x => x.Id == confirmItem.ItemId)
                ?? throw new NotFoundException($"Material request item {confirmItem.ItemId} not found");

            item.ActualQuantity = confirmItem.ActualQuantity;
            _unitOfWork.MaterialRequestItems.Update(item);

            if (confirmItem.ActualQuantity < item.ShortfallQuantity)
                stillShort.Add((item, item.ShortfallQuantity - confirmItem.ActualQuantity));
        }

        request.Status = MaterialRequestStatus.Fulfilled;
        request.FulfilledByQCId = qcId;
        request.FulfilledAt = DateTime.UtcNow;
        _unitOfWork.MaterialRequests.Update(request);

        MaterialRequest? nextRequest = null;
        if (stillShort.Count > 0)
        {
            if (request.Round >= MaxSupplementalRounds)
                throw new BusinessRuleException(
                    $"Maximum supplemental rounds ({MaxSupplementalRounds}) reached — cannot create another request");

            nextRequest = new MaterialRequest
            {
                OriginalDeliveryId = request.OriginalDeliveryId,
                BatchId = request.BatchId,
                WorkshopId = request.WorkshopId,
                Status = MaterialRequestStatus.Pending,
                CreatedByQCId = qcId,
                Round = request.Round + 1,
                IsAdHoc = request.IsAdHoc,
                Reason = request.Reason
            };
            await _unitOfWork.MaterialRequests.AddAsync(nextRequest);
            await _unitOfWork.SaveChangesAsync();

            foreach (var (item, shortfall) in stillShort)
            {
                await _unitOfWork.MaterialRequestItems.AddAsync(new MaterialRequestItem
                {
                    MaterialRequestId = nextRequest.Id,
                    MaterialName = item.MaterialName,
                    Unit = item.Unit,
                    ShortfallQuantity = shortfall
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();

        var batch = await _unitOfWork.Batches.GetByIdAsync(request.BatchId);
        if (batch?.AssignedToLeadId is int leadId)
        {
            await _notifications.NotifyMaterialRequestFulfilledAsync(
                leadId, request.BatchId, targetWorkshopId, new
                {
                    RequestId = request.Id,
                    BatchId = request.BatchId,
                    WorkshopId = targetWorkshopId,
                    Round = request.Round,
                    NewRequestId = nextRequest?.Id
                });

            if (nextRequest != null)
            {
                await _notifications.NotifyMaterialShortfallAsync(
                    leadId, request.BatchId, targetWorkshopId, new
                    {
                        RequestId = nextRequest.Id,
                        BatchId = request.BatchId,
                        WorkshopId = targetWorkshopId,
                        Round = nextRequest.Round,
                        Items = stillShort.Select(s => new
                        {
                            MaterialName = s.item.MaterialName,
                            ShortfallQuantity = s.shortfall
                        }).ToList()
                    });
            }
        }

        return nextRequest != null
            ? await MapToDtoAsync(nextRequest.Id)
            : await MapToDtoAsync(request.Id);
    }

    private async Task<MaterialRequestDto> MapToDtoAsync(int id)
    {
        var r = await _unitOfWork.MaterialRequests.FirstOrDefaultAsync(
            x => x.Id == id,
            new[] { "Batch", "OriginalDelivery.Workshop", "Workshop", "Items", "CreatedByQC", "ApprovedByLead", "FulfilledByQC" })
            ?? throw new NotFoundException("Material request not found");
        return MapToDtoValue(r);
    }

    private static MaterialRequestDto MapToDtoValue(MaterialRequest r) => new(
        r.Id,
        r.OriginalDeliveryId ?? 0,
        r.BatchId,
        r.Batch?.BatchNumber ?? "",
        r.OriginalDelivery?.WorkshopId ?? r.WorkshopId,
        r.OriginalDelivery?.Workshop?.Name ?? r.Workshop?.Name ?? "",
        r.Status.ToString(),
        r.CreatedByQCId,
        r.CreatedByQC?.Name ?? "",
        r.CreatedAt,
        r.ApprovedByLeadId,
        r.ApprovedByLead?.Name,
        r.ApprovedAt,
        r.FulfilledByQCId,
        r.FulfilledByQC?.Name,
        r.FulfilledAt,
        r.Round,
        r.IsAdHoc,
        r.Reason,
        r.Items
            .OrderBy(i => i.Id)
            .Select(i => new MaterialRequestItemDto(
                i.Id, i.MaterialName, i.Unit, i.ShortfallQuantity, i.ActualQuantity))
            .ToList());
}