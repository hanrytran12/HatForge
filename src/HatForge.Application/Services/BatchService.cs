using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class BatchService : IBatchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;

    public BatchService(IUnitOfWork unitOfWork, INotificationPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<BatchDto> CreateBatchAsync(CreateBatchDto dto)
    {
        if (dto.TargetQuantity <= 0)
            throw new BusinessRuleException("Target quantity must be greater than 0");

        if (dto.StartDate < DateTime.UtcNow.Date)
            throw new BusinessRuleException("Start date cannot be in the past");

        if (dto.EndDate.Date <= dto.StartDate.Date)
            throw new BusinessRuleException("End date must be after start date");

        var hatModel = await _unitOfWork.HatModels.GetByIdAsync(dto.HatModelId)
            ?? throw new NotFoundException("HatModel not found");
        var lead = await _unitOfWork.Users.FirstOrDefaultAsync(x => x.Id == dto.AssignToLeadId && x.Role == UserRole.Lead)
            ?? throw new NotFoundException("Lead not found");

        var batchNumber = await GenerateUniqueBatchNumberAsync();

        var batch = new Batch
        {
            BatchNumber = batchNumber,
            HatModelId = dto.HatModelId,
            TargetQuantity = dto.TargetQuantity,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Status = BatchStatus.Assigned,
            AssignedToLeadId = dto.AssignToLeadId
        };

        await _unitOfWork.Batches.AddAsync(batch);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyBatchAssignedToLeadAsync(dto.AssignToLeadId, new
        {
            BatchId = batch.Id,
            batch.BatchNumber,
            batch.TargetQuantity,
            StartDate = batch.StartDate,
            EndDate = batch.EndDate,
            LeadId = dto.AssignToLeadId
        });

        return await GetBatchByIdAsync(batch.Id) ?? throw new NotFoundException("Batch not found");
    }

    public async Task<BatchDto> PlanBatchAsync(int batchId, PlanBatchDto dto, int leadId)
    {
        var batch = await _unitOfWork.Batches.GetByIdAsync(batchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.AssignedToLeadId != leadId)
            throw new ForbiddenException("Only the assigned lead can plan this batch");

        if (batch.Status != BatchStatus.Assigned)
            throw new BusinessRuleException("Batch can only be planned when in Assigned status");

        if (dto.Workshops == null || dto.Workshops.Count == 0)
            throw new BusinessRuleException("At least one workshop must be in the plan");

        // Guard against null items in the list
        if (dto.Workshops.Any(w => w == null))
            throw new BusinessRuleException("Workshop plan contains invalid entries");

        // Guard against duplicate workshopId
        var duplicateWorkshop = dto.Workshops
            .GroupBy(w => w.WorkshopId)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateWorkshop != null)
            throw new BusinessRuleException($"Workshop {duplicateWorkshop.Key} appears more than once in the plan");

        // Guard against duplicate orderIndex
        var duplicateOrder = dto.Workshops
            .GroupBy(w => w.OrderIndex)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicateOrder != null)
            throw new BusinessRuleException($"OrderIndex {duplicateOrder.Key} is used by more than one workshop");

        // Validate all workshops exist
        foreach (var item in dto.Workshops)
        {
            var workshop = await _unitOfWork.Workshops.GetByIdAsync(item.WorkshopId)
                ?? throw new NotFoundException($"Workshop {item.WorkshopId} not found");

            if (item.RequiresMaterials && item.MaterialDeliveryDate == null)
                throw new BusinessRuleException(
                    $"Workshop {item.WorkshopId} requires materials — MaterialDeliveryDate must be provided");

            if (item.RequiresMaterials && (item.MaterialItems == null || item.MaterialItems.Count == 0))
                throw new BusinessRuleException(
                    $"Workshop {item.WorkshopId} requires materials — at least one material item must be provided");

            if (item.RequiresMaterials && item.EstimatedMetersPerUnit <= 0)
                throw new BusinessRuleException(
                    $"Workshop {item.WorkshopId} requires materials — EstimatedMetersPerUnit must be greater than 0");

            if (item.EndDate.Date <= item.StartDate.Date)
                throw new BusinessRuleException(
                    $"Workshop {item.WorkshopId}: end date must be after start date");

            if (item.StartDate.Date < batch.StartDate.Date || item.EndDate.Date > batch.EndDate.Date)
                throw new BusinessRuleException(
                    $"Workshop {item.WorkshopId}: dates must be within batch range ({batch.StartDate:yyyy-MM-dd} – {batch.EndDate:yyyy-MM-dd})");

            if (item.RequiresMaterials && item.MaterialDeliveryDate.HasValue)
            {
                var deliveryDate = item.MaterialDeliveryDate.Value.Date;
                if (deliveryDate < batch.StartDate.Date || deliveryDate > item.StartDate.Date)
                    throw new BusinessRuleException(
                        $"Workshop {item.WorkshopId}: material delivery date must be within [{batch.StartDate:yyyy-MM-dd} – {item.StartDate:yyyy-MM-dd}]");
            }
        }

        var existingBws = await _unitOfWork.BatchWorkshops.FindAsync(x => x.BatchId == batchId);
        foreach (var bw in existingBws)
            _unitOfWork.BatchWorkshops.Remove(bw);

        // Normalize orderIndex to 0-based sequential regardless of what client sent
        var orderedWorkshops = dto.Workshops.OrderBy(x => x.OrderIndex).ToList();
        foreach (var (item, index) in orderedWorkshops.Select((w, i) => (w, i)))
        {
            var bw = new BatchWorkshop
            {
                BatchId = batchId,
                WorkshopId = item.WorkshopId,
                OrderIndex = index,  // always 0-based sequential
                RequiresMaterials = item.RequiresMaterials,
                MaterialsReceived = false,
                IsCompleted = false,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                EstimatedMetersPerUnit = item.RequiresMaterials ? item.EstimatedMetersPerUnit : 0m
            };
            await _unitOfWork.BatchWorkshops.AddAsync(bw);

            if (item.RequiresMaterials && item.MaterialDeliveryDate.HasValue)
            {
                var delivery = new MaterialDelivery
                {
                    BatchId = batchId,
                    WorkshopId = item.WorkshopId,
                    ScheduledDate = item.MaterialDeliveryDate.Value,
                    Status = MaterialDeliveryStatus.Scheduled
                };
                await _unitOfWork.MaterialDeliveries.AddAsync(delivery);
                await _unitOfWork.SaveChangesAsync(); // get delivery.Id

                foreach (var mat in item.MaterialItems!)
                {
                    await _unitOfWork.MaterialDeliveryItems.AddAsync(new MaterialDeliveryItem
                    {
                        MaterialDeliveryId = delivery.Id,
                        MaterialName = mat.MaterialName,
                        PlannedQuantity = mat.PlannedQuantity
                    });
                }
            }
        }

        batch.Status = BatchStatus.InProduction;
        _unitOfWork.Batches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        // Notify QC of each assigned workshop
        foreach (var item in orderedWorkshops)
        {
            await _notifications.NotifyBatchPlannedAsync(item.WorkshopId, new
            {
                BatchId = batchId,
                batch.BatchNumber,
                WorkshopId = item.WorkshopId,
                StartDate = item.StartDate,
                EndDate = item.EndDate,
                RequiresMaterials = item.RequiresMaterials
            });
        }

        return await GetBatchByIdAsync(batchId) ?? throw new NotFoundException("Batch not found");
    }

    public async Task<BatchDto?> GetBatchByIdAsync(int id)
    {
        var batch = await _unitOfWork.Batches.FirstOrDefaultAsync(
            x => x.Id == id,
            new[] { "HatModel", "AssignedToLead", "BatchWorkshops.Workshop" });

        if (batch == null) return null;
        return MapToDto(batch);
    }

    public async Task<IReadOnlyList<BatchListDto>> GetAllBatchesAsync()
    {
        var batches = await _unitOfWork.Batches.FindAsync(x => true,
            new[] { "HatModel" });
        return batches
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BatchListDto(
                x.Id, x.BatchNumber, x.HatModel?.Name ?? "",
                x.Status.ToString(), x.StartDate, x.EndDate, x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<BatchListDto>> GetBatchesByLeadAsync(int leadId)
    {
        var batches = await _unitOfWork.Batches.FindAsync(
            x => x.AssignedToLeadId == leadId,
            new[] { "HatModel" });
        return batches
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BatchListDto(
                x.Id, x.BatchNumber, x.HatModel?.Name ?? "",
                x.Status.ToString(), x.StartDate, x.EndDate, x.CreatedAt))
            .ToList();
    }

    public async Task<BatchDto> MarkWorkshopCompletedAsync(int batchId, int workshopId)
    {
        var bw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == batchId && x.WorkshopId == workshopId)
            ?? throw new NotFoundException("BatchWorkshop not found");

        if (bw.IsCompleted)
            throw new BusinessRuleException("Workshop is already marked as completed");

        var batch = await _unitOfWork.Batches.GetByIdAsync(batchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.Status == BatchStatus.Assigned)
            throw new BusinessRuleException("Batch has not been planned yet. Lead must plan workshops first");

        bw.IsCompleted = true;
        _unitOfWork.BatchWorkshops.Update(bw);

        var allWorkshops = await _unitOfWork.BatchWorkshops.FindAsync(x => x.BatchId == batchId);
        var allCompleted = allWorkshops.Count > 0 && allWorkshops.All(x => x.IsCompleted);

        if (allCompleted)
        {
            // All workshops done — go through Lead review, not directly to Completed
            batch.Status = BatchStatus.PendingLeadReview;
        }
        else
        {
            batch.Status = BatchStatus.ReadyForTransfer;
        }

        _unitOfWork.Batches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        if (allCompleted && batch.AssignedToLeadId.HasValue)
            await _notifications.NotifyFinalReviewRequestedAsync(batch.AssignedToLeadId.Value, new
            {
                BatchId = batchId,
                batch.BatchNumber,
                Message = "Xưởng cuối đã xong, cần bạn review."
            });

        return await GetBatchByIdAsync(batchId) ?? throw new NotFoundException("Batch not found");
    }

    public async Task<BatchDto> LeadApproveFinalAsync(int batchId, int leadId)
    {
        var batch = await _unitOfWork.Batches.GetByIdAsync(batchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.AssignedToLeadId != leadId)
            throw new ForbiddenException("Only the assigned lead can approve this batch");

        if (batch.Status != BatchStatus.PendingLeadReview)
            throw new BusinessRuleException("Batch is not pending lead review");

        batch.Status = BatchStatus.PendingGateQC;
        _unitOfWork.Batches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyGateQCReviewRequestedAsync(new
        {
            BatchId = batch.Id,
            batch.BatchNumber,
            Message = "Lead has approved. Please perform final quality check."
        });

        return await GetBatchByIdAsync(batchId) ?? throw new NotFoundException("Batch not found");
    }

    public async Task<BatchDto> GateConfirmAsync(int batchId, int qcGateId)
    {
        var qcGate = await _unitOfWork.Users.GetByIdAsync(qcGateId)
            ?? throw new NotFoundException("User not found");

        if (qcGate.Role != UserRole.QCGate)
            throw new ForbiddenException("Only QC Gate can perform final confirmation");

        var batch = await _unitOfWork.Batches.GetByIdAsync(batchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.Status != BatchStatus.PendingGateQC)
            throw new BusinessRuleException("Batch is not pending gate QC confirmation");

        // Auto-compute CompletedQuantity from passed quantities at the last workshop.
        var batchWorkshops = await _unitOfWork.BatchWorkshops.FindAsync(x => x.BatchId == batchId);
        var lastBw = batchWorkshops.OrderByDescending(x => x.OrderIndex).FirstOrDefault();
        int? completedQuantity = null;
        if (lastBw != null)
        {
            var lastWorks = await _unitOfWork.Works.FindAsync(
                x => x.BatchId == batchId
                  && x.WorkshopId == lastBw.WorkshopId);
            completedQuantity = lastWorks.Sum(w => w.PassedQuantity);
        }

        batch.Status = BatchStatus.Completed;
        batch.CompletedAt = DateTime.UtcNow;
        batch.CompletedQuantity = completedQuantity;
        _unitOfWork.Batches.Update(batch);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyBatchCompletedAsync(batchId, batch.AssignedToLeadId,
            new { BatchId = batchId, batch.BatchNumber });

        return await GetBatchByIdAsync(batchId) ?? throw new NotFoundException("Batch not found");
    }

    public async Task<BatchFinalSummaryDto> GetFinalSummaryAsync(int batchId)
    {
        var batch = await _unitOfWork.Batches.FirstOrDefaultAsync(
            x => x.Id == batchId,
            new[] { "HatModel" })
            ?? throw new NotFoundException("Batch not found");

        var batchWorkshops = await _unitOfWork.BatchWorkshops.FindAsync(
            x => x.BatchId == batchId,
            new[] { "Workshop" });

        var allWorks = await _unitOfWork.Works.FindAsync(x => x.BatchId == batchId);
        var allTransfers = await _unitOfWork.TransferRequests.FindAsync(x => x.BatchId == batchId);
        var allMaterialRequests = await _unitOfWork.MaterialRequests.FindAsync(x => x.BatchId == batchId);

        var workshopDtos = batchWorkshops
            .OrderBy(x => x.OrderIndex)
            .Select(bw =>
            {
                var works = allWorks.Where(w => w.WorkshopId == bw.WorkshopId).ToList();
                var counts = new FinalSummaryWorkCountsDto(
                    works.Count(w => w.Status == WorkStatus.Submitted),
                    works.Count(w => w.Status == WorkStatus.Approved),
                    works.Count(w => w.Status == WorkStatus.Rejected),
                    works.Sum(w => w.PassedQuantity));

                var material = new FinalSummaryMaterialUsageDto(
                    bw.InitialMaterialQty,
                    bw.MaterialUsed,
                    bw.InitialMaterialQty - bw.MaterialUsed);

                return new FinalSummaryWorkshopDto(
                    bw.WorkshopId,
                    bw.Workshop?.Name ?? "",
                    bw.OrderIndex,
                    bw.IsCompleted,
                    bw.RequiresMaterials,
                    bw.MaterialsReceived,
                    counts,
                    material);
            })
            .ToList();

        var transferCounts = new FinalSummaryTransferCountsDto(
            allTransfers.Count(t => t.Status == TransferStatus.Pending),
            allTransfers.Count(t => t.Status == TransferStatus.Approved),
            allTransfers.Count(t => t.Status == TransferStatus.Transferred),
            allTransfers.Count);

        var materialRequestCounts = new FinalSummaryMaterialRequestCountsDto(
            allMaterialRequests.Count(r => r.Status == MaterialRequestStatus.Pending),
            allMaterialRequests.Count(r => r.Status == MaterialRequestStatus.Approved),
            allMaterialRequests.Count(r => r.Status == MaterialRequestStatus.Fulfilled),
            allMaterialRequests.Count);

        return new BatchFinalSummaryDto(
            batch.Id,
            batch.BatchNumber,
            batch.Status.ToString(),
            batch.TargetQuantity,
            batch.CompletedQuantity,
            workshopDtos,
            transferCounts,
            materialRequestCounts);
    }

    public async Task<IReadOnlyList<BatchListDto>> GetBatchesByStatusAsync(BatchStatus status)
    {
        var batches = await _unitOfWork.Batches.FindAsync(
            x => x.Status == status,
            new[] { "HatModel" });
        return batches
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new BatchListDto(
                x.Id, x.BatchNumber, x.HatModel?.Name ?? "",
                x.Status.ToString(), x.StartDate, x.EndDate, x.CreatedAt))
            .ToList();
    }

    private static BatchDto MapToDto(Batch batch) => new(
        batch.Id,
        batch.BatchNumber,
        batch.HatModelId,
        batch.HatModel?.Name ?? "",
        batch.Status.ToString(),
        batch.AssignedToLeadId,
        batch.AssignedToLead?.Name,
        batch.TargetQuantity,
        batch.StartDate,
        batch.EndDate,
        batch.CreatedAt,
        batch.CompletedAt,
        batch.CompletedQuantity,
        batch.BatchWorkshops?
            .OrderBy(x => x.OrderIndex)
            .Select(x => new WorkshopInBatchDto(
                x.WorkshopId, x.Workshop?.Name ?? "", x.OrderIndex,
                x.RequiresMaterials, x.MaterialsReceived, x.IsCompleted,
                x.StartDate, x.EndDate,
                x.InitialMaterialQty, x.MaterialUsed,
                x.InitialMaterialQty - x.MaterialUsed,
                x.EstimatedMetersPerUnit))
            .ToList() ?? new()
    );

    private async Task<string> GenerateUniqueBatchNumberAsync()
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"BATCH-{today}-";

        var todayBatches = await _unitOfWork.Batches.FindAsync(
            x => x.BatchNumber.StartsWith(prefix));

        var sequence = todayBatches.Count + 1;

        string batchNumber;
        do
        {
            batchNumber = $"{prefix}{sequence:D4}";
            var exists = await _unitOfWork.Batches.FirstOrDefaultAsync(x => x.BatchNumber == batchNumber);
            if (exists == null) break;
            sequence++;
        } while (true);

        return batchNumber;
    }
}
