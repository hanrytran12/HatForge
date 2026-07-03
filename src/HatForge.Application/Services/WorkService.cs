using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class WorkService : IWorkService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;

    public WorkService(IUnitOfWork unitOfWork, INotificationPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<WorkDto> SubmitWorkAsync(SubmitWorkDto dto, int staffId)
    {
        if (dto.BatchId <= 0)
            throw new BusinessRuleException("Valid batch is required");

        if (dto.WorkshopId <= 0)
            throw new BusinessRuleException("Valid workshop is required");

        if (dto.Quantity <= 0)
            throw new BusinessRuleException("Quantity must be greater than 0");

        if (dto.PhotoUrls == null || dto.PhotoUrls.Count == 0)
            throw new BusinessRuleException("At least one photo is required");

        if (dto.PhotoUrls.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 512))
            throw new BusinessRuleException("Photo URL is invalid");

        var batch = await _unitOfWork.Batches.GetByIdAsync(dto.BatchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.Status == BatchStatus.Assigned)
            throw new BusinessRuleException("Batch has not been planned yet");

        if (batch.Status is BatchStatus.Created or BatchStatus.Completed or BatchStatus.PendingLeadReview or BatchStatus.PendingGateQC)
            throw new BusinessRuleException("Batch is not accepting work submissions");

        var staff = await _unitOfWork.Users.GetByIdAsync(staffId)
            ?? throw new NotFoundException("Staff not found");

        if (staff.Role != UserRole.Staff)
            throw new ForbiddenException("Only staff can submit work");

        // Staff must belong to the workshop they are submitting for
        if (staff.WorkshopId != dto.WorkshopId)
            throw new ForbiddenException("You can only submit work for your own workshop");

        var workshopBw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == dto.WorkshopId,
            new[] { "Workshop" })
            ?? throw new NotFoundException("Workshop not in this batch");

        if (workshopBw.IsCompleted)
            throw new BusinessRuleException("Workshop is already completed for this batch");

        // Check materials received if workshop requires them
        if (workshopBw.RequiresMaterials && !workshopBw.MaterialsReceived)
            throw new BusinessRuleException("Workshop has not received materials yet");

        // Guard: cannot submit if no material remaining
        if (workshopBw.RequiresMaterials && workshopBw.InitialMaterialQty - workshopBw.MaterialUsed <= 0)
            throw new BusinessRuleException("Không có đủ nguyên vật liệu để làm việc");

        var estimatedUsage = workshopBw.RequiresMaterials
            ? dto.Quantity * workshopBw.EstimatedMetersPerUnit
            : 0m;

        if (workshopBw.RequiresMaterials
            && workshopBw.InitialMaterialQty - workshopBw.MaterialUsed < estimatedUsage)
            throw new BusinessRuleException(
                $"Không đủ nguyên vật liệu. Cần {Math.Round(estimatedUsage, 0, MidpointRounding.AwayFromZero)}m nhưng chỉ còn {Math.Round(workshopBw.InitialMaterialQty - workshopBw.MaterialUsed, 0, MidpointRounding.AwayFromZero)}m");

        // Workshops after the first in the chain must wait for a transfer from the previous workshop
        if (workshopBw.OrderIndex > 0)
        {
            var allBatchWorkshops = await _unitOfWork.BatchWorkshops.FindAsync(
                x => x.BatchId == dto.BatchId);
            var previousBw = allBatchWorkshops.FirstOrDefault(x => x.OrderIndex == workshopBw.OrderIndex - 1);

            if (previousBw != null)
            {
                var transferReceived = await _unitOfWork.TransferRequests.FirstOrDefaultAsync(
                    x => x.BatchId == dto.BatchId
                      && x.FromWorkshopId == previousBw.WorkshopId
                      && x.ToWorkshopId == dto.WorkshopId
                      && x.Status == TransferStatus.Transferred);

                if (transferReceived == null)
                    throw new BusinessRuleException(
                        "Cannot submit work — batch has not been transferred from the previous workshop yet");

                if (!dto.IsRework)
                {
                    var availableInputQuantity = transferReceived.ReceivedUsableQuantity
                        ?? await GetPassedQuantityAsync(dto.BatchId, previousBw.WorkshopId);
                    var existingNonReworkQuantity = await GetSubmittedNonReworkQuantityAsync(dto.BatchId, dto.WorkshopId);
                    var remainingInputQuantity = availableInputQuantity - existingNonReworkQuantity;
                    if (dto.Quantity > remainingInputQuantity)
                        throw new BusinessRuleException(
                            $"Cannot submit {dto.Quantity} items because only {remainingInputQuantity} received usable items remain for this workshop");
                }
            }
        }

        // Prevent submitting new work while a previous submission is still pending QC review
        var pendingWork = await _unitOfWork.Works.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId
              && x.WorkshopId == dto.WorkshopId
              && x.Status == WorkStatus.Submitted);
        if (pendingWork != null)
            throw new BusinessRuleException("There is already a pending work submission awaiting QC review for this workshop");

        var existingWorks = await _unitOfWork.Works.FindAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == dto.WorkshopId);
        if (dto.IsRework)
        {
            var repairableRemaining = CalculateRepairableRemaining(existingWorks);
            if (repairableRemaining <= 0)
                throw new BusinessRuleException("There is no repairable work remaining to resubmit for this workshop");
            if (dto.Quantity > repairableRemaining)
                throw new BusinessRuleException(
                    $"Cannot resubmit {dto.Quantity} items because only {repairableRemaining} repairable items remain");
        }

        var work = new Work
        {
            BatchId = dto.BatchId,
            WorkshopId = dto.WorkshopId,
            StaffId = staffId,
            Quantity = dto.Quantity,
            IsRework = dto.IsRework,
            Status = WorkStatus.Submitted,
            EstimatedMaterialUsed = estimatedUsage
        };

        await _unitOfWork.Works.AddAsync(work);

        batch.Status = BatchStatus.UnderQCReview;
        _unitOfWork.Batches.Update(batch);

        if (workshopBw.RequiresMaterials && estimatedUsage > 0)
        {
            workshopBw.MaterialUsed += estimatedUsage;
            _unitOfWork.BatchWorkshops.Update(workshopBw);
        }

        await _unitOfWork.SaveChangesAsync();

        foreach (var url in dto.PhotoUrls)
        {
            await _unitOfWork.WorkPhotos.AddAsync(new WorkPhoto
            {
                WorkId = work.Id,
                PhotoUrl = url,
                Type = WorkPhotoType.Submission
            });
        }

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkSubmittedAsync(dto.BatchId, dto.WorkshopId,
            new { WorkId = work.Id, Quantity = work.Quantity });

        return await MapToDto(work.Id);
    }

    public async Task<WorkDto> ApproveWorkAsync(ApproveWorkDto dto, int qcId)
    {
        if (dto.ActualMaterialUsed < 0)
            throw new BusinessRuleException("ActualMaterialUsed must be greater than or equal to 0");

        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can approve work");

        var work = await _unitOfWork.Works.GetByIdAsync(dto.WorkId)
            ?? throw new NotFoundException("Work not found");

        if (qc.WorkshopId != work.WorkshopId)
            throw new ForbiddenException("You can only approve work for your own workshop");

        var batch = await _unitOfWork.Batches.GetByIdAsync(work.BatchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.Status is BatchStatus.Completed or BatchStatus.PendingLeadReview or BatchStatus.PendingGateQC)
            throw new BusinessRuleException("Batch is not accepting QC reviews");

        if (work.Status != WorkStatus.Submitted)
            throw new BusinessRuleException("Work is not in submitted state");

        work.Status = WorkStatus.Approved;
        work.ReviewedByQCId = qcId;
        work.ReviewedAt = DateTime.UtcNow;
        work.ActualMaterialUsed = dto.ActualMaterialUsed;
        work.PassedQuantity = work.Quantity;
        work.RepairableQuantity = 0;
        work.UnrepairableQuantity = 0;
        _unitOfWork.Works.Update(work);

        batch.Status = BatchStatus.ReadyForTransfer;
        _unitOfWork.Batches.Update(batch);

        var remainingAfter = await ReconcileMaterialUsageAsync(work, dto.ActualMaterialUsed);

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkApprovedAsync(work.BatchId, work.StaffId, new { WorkId = work.Id });

        if (remainingAfter.HasValue)
            await NotifyMaterialLowIfNeededAsync(work.BatchId, work.WorkshopId, remainingAfter.Value);

        return await MapToDto(dto.WorkId);
    }

    public async Task<WorkDto> RejectWorkAsync(RejectWorkDto dto, int qcId)
    {
        var rejectionPhotoUrls = dto.PhotoUrls ?? new List<string>();

        if (string.IsNullOrWhiteSpace(dto.RejectionNotes) || dto.RejectionNotes.Length > 500)
            throw new BusinessRuleException("Rejection notes are required and must be 500 characters or fewer");

        if (dto.PassedQuantity < 0 || dto.RepairableQuantity < 0 || dto.UnrepairableQuantity < 0)
            throw new BusinessRuleException("QC quantities must be greater than or equal to 0");

        if (dto.ActualMaterialUsed < 0)
            throw new BusinessRuleException("ActualMaterialUsed must be greater than or equal to 0");

        if (rejectionPhotoUrls.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 512))
            throw new BusinessRuleException("Photo URL is invalid");

        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can reject work");

        var work = await _unitOfWork.Works.GetByIdAsync(dto.WorkId)
            ?? throw new NotFoundException("Work not found");

        if (qc.WorkshopId != work.WorkshopId)
            throw new ForbiddenException("You can only reject work for your own workshop");

        var batch = await _unitOfWork.Batches.GetByIdAsync(work.BatchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.Status is BatchStatus.Completed or BatchStatus.PendingLeadReview or BatchStatus.PendingGateQC)
            throw new BusinessRuleException("Batch is not accepting QC reviews");

        if (work.Status != WorkStatus.Submitted)
            throw new BusinessRuleException("Work is not in submitted state");

        var reviewedQuantity = dto.PassedQuantity + dto.RepairableQuantity + dto.UnrepairableQuantity;
        if (reviewedQuantity != work.Quantity)
            throw new BusinessRuleException(
                $"QC quantities must add up to submitted quantity ({work.Quantity})");

        work.Status = WorkStatus.Rejected;
        work.RejectionNotes = dto.RejectionNotes;
        work.PassedQuantity = dto.PassedQuantity;
        work.RepairableQuantity = dto.RepairableQuantity;
        work.UnrepairableQuantity = dto.UnrepairableQuantity;
        work.ReviewedByQCId = qcId;
        work.ReviewedAt = DateTime.UtcNow;
        work.ActualMaterialUsed = dto.ActualMaterialUsed;
        _unitOfWork.Works.Update(work);

        batch.Status = BatchStatus.InProduction;
        _unitOfWork.Batches.Update(batch);

        var remainingAfter = await ReconcileMaterialUsageAsync(work, dto.ActualMaterialUsed);

        await _unitOfWork.SaveChangesAsync();

        foreach (var url in rejectionPhotoUrls)
        {
            await _unitOfWork.WorkPhotos.AddAsync(new WorkPhoto
            {
                WorkId = work.Id,
                PhotoUrl = url,
                Type = WorkPhotoType.Rejection
            });
        }

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkRejectedAsync(work.BatchId, work.StaffId, new
        {
            WorkId = work.Id,
            dto.PassedQuantity,
            dto.RepairableQuantity,
            dto.UnrepairableQuantity
        });

        if (remainingAfter.HasValue)
            await NotifyMaterialLowIfNeededAsync(work.BatchId, work.WorkshopId, remainingAfter.Value);

        return await MapToDto(dto.WorkId);
    }

    public async Task<IReadOnlyList<WorkDto>> GetWorksByBatchAsync(int batchId)
    {
        var works = await _unitOfWork.Works.FindAsync(x => x.BatchId == batchId,
            new[] { "Workshop", "Staff", "ReviewedByQC", "Photos" });

        return works.Select(MapToDtoValue).ToList();
    }

    public async Task<IReadOnlyList<WorkDto>> GetWorksByBatchAndWorkshopAsync(int batchId, int workshopId)
    {
        var works = await _unitOfWork.Works.FindAsync(
            x => x.BatchId == batchId && x.WorkshopId == workshopId,
            new[] { "Workshop", "Staff", "ReviewedByQC", "Photos" });

        return works.Select(MapToDtoValue).ToList();
    }

    private async Task<WorkDto> MapToDto(int workId)
    {
        var work = await _unitOfWork.Works.FirstOrDefaultAsync(x => x.Id == workId,
            new[] { "Workshop", "Staff", "ReviewedByQC", "Photos" })
            ?? throw new NotFoundException("Work not found");
        return MapToDtoValue(work);
    }

    private static int CalculateRepairableRemaining(IEnumerable<Work> works)
    {
        var remaining = 0;
        foreach (var work in works.OrderBy(x => x.SubmittedDate).ThenBy(x => x.Id))
        {
            if (work.IsRework)
            {
                var consumedRepairable = Math.Min(remaining, work.Quantity);
                remaining -= consumedRepairable;
            }

            if (work.Status == WorkStatus.Rejected)
                remaining += work.RepairableQuantity;
        }

        return remaining;
    }

    private async Task<int> GetPassedQuantityAsync(int batchId, int workshopId)
    {
        var works = await _unitOfWork.Works.FindAsync(
            x => x.BatchId == batchId && x.WorkshopId == workshopId);
        return works.Sum(x => x.PassedQuantity);
    }

    private async Task<int> GetSubmittedNonReworkQuantityAsync(int batchId, int workshopId)
    {
        var works = await _unitOfWork.Works.FindAsync(
            x => x.BatchId == batchId && x.WorkshopId == workshopId && !x.IsRework);
        return works.Sum(x => x.Quantity);
    }

    private async Task<decimal?> ReconcileMaterialUsageAsync(Work work, decimal actualMaterialUsed)
    {
        var bw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == work.BatchId && x.WorkshopId == work.WorkshopId);

        if (bw == null || !bw.RequiresMaterials)
            return null;

        var estimate = work.EstimatedMaterialUsed ?? 0m;
        bw.MaterialUsed = bw.MaterialUsed - estimate + actualMaterialUsed;
        if (bw.MaterialUsed < 0) bw.MaterialUsed = 0;

        _unitOfWork.BatchWorkshops.Update(bw);
        return bw.InitialMaterialQty - bw.MaterialUsed;
    }

    private async Task NotifyMaterialLowIfNeededAsync(int batchId, int workshopId, decimal materialRemaining)
    {
        if (materialRemaining > MaterialTracking.LowMaterialThresholdMeters)
            return;

        await _notifications.NotifyMaterialLowAlertAsync(
            batchId, workshopId,
            await BuildMaterialLowAlertPayloadAsync(
                batchId,
                workshopId,
                materialRemaining));
    }

    private async Task<MaterialLowAlertPayload> BuildMaterialLowAlertPayloadAsync(
        int batchId,
        int workshopId,
        decimal materialRemaining)
    {
        var deliveredMaterials = new List<MaterialLowAlertItemDto>();

        var deliveries = await _unitOfWork.MaterialDeliveries.FindAsync(
            x => x.BatchId == batchId
              && x.WorkshopId == workshopId
              && x.IsConfirmed,
            new[] { "Items" });

        deliveredMaterials.AddRange(deliveries
            .SelectMany(d => d.Items)
            .Where(i => i.ActualQuantity > 0)
            .Select(i => new MaterialLowAlertItemDto(i.MaterialName, i.ActualQuantity)));

        var fulfilledRequests = await _unitOfWork.MaterialRequests.FindAsync(
            x => x.BatchId == batchId
              && x.WorkshopId == workshopId
              && x.Status == MaterialRequestStatus.Fulfilled,
            new[] { "Items" });

        deliveredMaterials.AddRange(fulfilledRequests
            .SelectMany(r => r.Items)
            .Where(i => i.ActualQuantity.HasValue && i.ActualQuantity.Value > 0)
            .Select(i => new MaterialLowAlertItemDto(i.MaterialName, i.ActualQuantity!.Value)));

        var materials = deliveredMaterials
            .GroupBy(i => i.MaterialName)
            .Select(g => new MaterialLowAlertItemDto(g.Key, g.Sum(i => i.ActualQuantity)))
            .OrderBy(i => i.MaterialName)
            .ToList();

        return new MaterialLowAlertPayload(
            batchId,
            workshopId,
            materialRemaining,
            MaterialTracking.LowMaterialThresholdMeters,
            materials);
    }

    private static WorkDto MapToDtoValue(Work w) => new(
        w.Id, w.BatchId, w.WorkshopId, w.Workshop?.Name ?? "",
        w.StaffId, w.Staff?.Name ?? "", w.Quantity, w.IsRework,
        w.Photos.Where(p => p.Type == WorkPhotoType.Submission).Select(p => p.PhotoUrl).ToList(),
        w.Photos.Where(p => p.Type == WorkPhotoType.Rejection).Select(p => p.PhotoUrl).ToList(),
        w.SubmittedDate, w.Status.ToString(),
        w.RejectionNotes,
        w.PassedQuantity,
        w.RepairableQuantity,
        w.UnrepairableQuantity,
        w.ReviewedByQCId, w.ReviewedAt,
        w.ActualMaterialUsed,
        w.EstimatedMaterialUsed
    );
}
