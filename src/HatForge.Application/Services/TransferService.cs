using HatForge.Application.DTOs;
using HatForge.Application.Common;
using HatForge.Application.Interfaces;
using HatForge.Domain.Entities;
using HatForge.Domain.Enums;
using HatForge.Domain.Interfaces;

namespace HatForge.Application.Services;

public class TransferService : ITransferService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INotificationPublisher _notifications;

    public TransferService(IUnitOfWork unitOfWork, INotificationPublisher notifications)
    {
        _unitOfWork = unitOfWork;
        _notifications = notifications;
    }

    public async Task<CreateTransferResultDto> CreateTransferRequestAsync(CreateTransferDto dto, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can create transfer requests");
        if (qc.WorkshopId == null)
            throw new ForbiddenException("You are not assigned to any workshop");

        var batch = await _unitOfWork.Batches.GetByIdAsync(dto.BatchId)
            ?? throw new NotFoundException("Batch not found");

        // Determine fromWorkshopId from QC's workshop
        var fromWorkshopId = qc.WorkshopId.Value;

        var allBatchWorkshops = await _unitOfWork.BatchWorkshops.FindAsync(
            x => x.BatchId == dto.BatchId);

        var fromBw = allBatchWorkshops.FirstOrDefault(x => x.WorkshopId == fromWorkshopId)
            ?? throw new NotFoundException("Your workshop is not part of this batch");

        if (fromBw.IsCompleted)
            throw new BusinessRuleException("Your workshop is already completed");

        // Auto-determine next workshop in chain
        var toBw = allBatchWorkshops
            .OrderBy(x => x.OrderIndex)
            .FirstOrDefault(x => x.OrderIndex == fromBw.OrderIndex + 1);

        // Load work records upfront (needed for both final and mid-chain cases)
        var works = await _unitOfWork.Works.FindAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == fromWorkshopId);

        // Last workshop in chain — trigger final review instead of transfer
        if (toBw == null)
        {
            if (GetPassedQuantity(works) <= 0)
                throw new BusinessRuleException("Your workshop has no passed work yet");
            if (works.Any(x => x.Status == WorkStatus.Submitted))
                throw new BusinessRuleException("Your workshop still has work pending QC review");
            if (CalculateRepairableRemaining(works) > 0)
                throw new BusinessRuleException("Your workshop still has repairable rejected work that must be resubmitted");

            fromBw.IsCompleted = true;
            _unitOfWork.BatchWorkshops.Update(fromBw);

            batch.Status = BatchStatus.PendingLeadReview;
            _unitOfWork.Batches.Update(batch);
            await _unitOfWork.SaveChangesAsync();

            if (batch.AssignedToLeadId.HasValue)
                await _notifications.NotifyFinalReviewRequestedAsync(batch.AssignedToLeadId.Value, new
                {
                    BatchId = batch.Id,
                    batch.BatchNumber,
                    Message = "All workshops completed. Please review and approve the final batch."
                });

            return new CreateTransferResultDto(true, null, BatchStatus.PendingLeadReview.ToString());
        }

        // Source workshop must have approved work and nothing pending QC review
        if (GetPassedQuantity(works) <= 0)
            throw new BusinessRuleException("Your workshop has no passed work yet");
        if (works.Any(x => x.Status == WorkStatus.Submitted))
            throw new BusinessRuleException("Your workshop still has work pending QC review");
        if (CalculateRepairableRemaining(works) > 0)
            throw new BusinessRuleException("Your workshop still has repairable rejected work that must be resubmitted");

        // Prevent duplicate active transfer request
        var existing = await _unitOfWork.TransferRequests.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId
              && x.FromWorkshopId == fromWorkshopId
              && x.ToWorkshopId == toBw.WorkshopId
              && (x.Status == TransferStatus.Pending || x.Status == TransferStatus.Approved));
        if (existing != null)
            throw new BusinessRuleException("There is already an active transfer request for this workshop hop");

        var transfer = new TransferRequest
        {
            BatchId = dto.BatchId,
            FromWorkshopId = fromWorkshopId,
            ToWorkshopId = toBw.WorkshopId,
            CreatedByQCId = qcId,
            Status = TransferStatus.Pending
        };

        await _unitOfWork.TransferRequests.AddAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        if (batch.AssignedToLeadId.HasValue)
        {
            var qualityIssues = await GetUnrepairableQualityIssuesAsync(dto.BatchId, fromWorkshopId);
            var approvedQuantity = GetPassedQuantity(works);
            await _notifications.NotifyTransferRequestedAsync(batch.AssignedToLeadId.Value,
                new
                {
                    TransferId = transfer.Id,
                    BatchId = dto.BatchId,
                    ApprovedQuantity = approvedQuantity,
                    QualityIssues = qualityIssues
                });
        }

        var transferDto = await MapToDto(transfer.Id);
        return new CreateTransferResultDto(false, transferDto, batch.Status.ToString());
    }

    public async Task<TransferRequestDto> ApproveTransferAsync(ApproveTransferDto dto, int leadId)
    {
        var lead = await _unitOfWork.Users.GetByIdAsync(leadId)
            ?? throw new NotFoundException("Lead not found");
        if (lead.Role != UserRole.Lead)
            throw new ForbiddenException("Only Lead can approve transfers");

        var transfer = await _unitOfWork.TransferRequests.GetByIdAsync(dto.TransferId)
            ?? throw new NotFoundException("Transfer request not found");

        if (transfer.Status != TransferStatus.Pending)
            throw new BusinessRuleException("Transfer is not pending");

        transfer.Status = TransferStatus.Approved;
        transfer.ApprovedByLeadId = leadId;
        transfer.ApprovedAt = DateTime.UtcNow;
        _unitOfWork.TransferRequests.Update(transfer);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyTransferApprovedAsync(transfer.BatchId, transfer.ToWorkshopId,
            new { TransferId = transfer.Id, transfer.BatchId, transfer.ToWorkshopId });

        return await MapToDto(transfer.Id);
    }

    public async Task<TransferRequestDto> ConfirmReceiptAsync(ConfirmReceiptDto dto, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can confirm receipt");

        var transfer = await _unitOfWork.TransferRequests.GetByIdAsync(dto.TransferId)
            ?? throw new NotFoundException("Transfer request not found");

        if (transfer.Status != TransferStatus.Approved)
            throw new BusinessRuleException("Transfer must be approved by Lead before it can be received");

        if (qc.WorkshopId != transfer.ToWorkshopId)
            throw new ForbiddenException("You can only confirm receipt for your own workshop");

        transfer.Status = TransferStatus.Transferred;
        transfer.ConfirmedByQCId = qcId;
        transfer.ConfirmedAt = DateTime.UtcNow;
        _unitOfWork.TransferRequests.Update(transfer);

        // Mark the source workshop as completed (successful) now that the next workshop has received it
        var fromBw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == transfer.BatchId && x.WorkshopId == transfer.FromWorkshopId);
        if (fromBw != null && !fromBw.IsCompleted)
        {
            fromBw.IsCompleted = true;
            _unitOfWork.BatchWorkshops.Update(fromBw);
        }

        var batch = await _unitOfWork.Batches.GetByIdAsync(transfer.BatchId)
            ?? throw new NotFoundException("Batch not found");

        var allWorkshops = await _unitOfWork.BatchWorkshops.FindAsync(x => x.BatchId == transfer.BatchId);
        // The last workshop in the chain uses CreateTransferRequest (no-next-workshop path),
        // so ConfirmReceiptAsync is only reached for mid-chain hops — always keep InProduction.
        batch.Status = BatchStatus.InProduction;
        _unitOfWork.Batches.Update(batch);

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkCanBeginAsync(transfer.ToWorkshopId, new
        {
            transfer.BatchId,
            TransferId = transfer.Id,
            transfer.ToWorkshopId
        });

        return await MapToDto(transfer.Id);
    }

    public async Task<IReadOnlyList<TransferRequestDto>> GetPendingTransfersAsync()
    {
        var transfers = await _unitOfWork.TransferRequests.FindAsync(
            x => x.Status == TransferStatus.Pending,
            new[] { "Batch", "FromWorkshop", "ToWorkshop" });

        var results = new List<TransferRequestDto>();
        foreach (var transfer in transfers)
            results.Add(await MapToDtoValueAsync(transfer));
        return results;
    }

    public async Task<IReadOnlyList<TransferRequestDto>> GetAwaitingReceiptByWorkshopAsync(int workshopId)
    {
        var transfers = await _unitOfWork.TransferRequests.FindAsync(
            x => x.Status == TransferStatus.Approved && x.ToWorkshopId == workshopId,
            new[] { "Batch", "FromWorkshop", "ToWorkshop" });

        var results = new List<TransferRequestDto>();
        foreach (var transfer in transfers)
            results.Add(await MapToDtoValueAsync(transfer));
        return results;
    }

    private async Task<TransferRequestDto> MapToDto(int id)
    {
        var t = await _unitOfWork.TransferRequests.FirstOrDefaultAsync(x => x.Id == id,
            new[] { "Batch", "FromWorkshop", "ToWorkshop" })
            ?? throw new NotFoundException("Transfer not found");
        return await MapToDtoValueAsync(t);
    }

    private async Task<TransferRequestDto> MapToDtoValueAsync(TransferRequest t)
    {
        var works = await _unitOfWork.Works.FindAsync(
            x => x.BatchId == t.BatchId && x.WorkshopId == t.FromWorkshopId);
        var approvedQuantity = GetPassedQuantity(works);
        var qualityIssues = await GetUnrepairableQualityIssuesAsync(t.BatchId, t.FromWorkshopId);

        return new TransferRequestDto(
            t.Id, t.BatchId, t.Batch?.BatchNumber ?? "",
            t.FromWorkshopId, t.FromWorkshop?.Name ?? "",
            t.ToWorkshopId, t.ToWorkshop?.Name ?? "",
            approvedQuantity,
            t.CreatedAt, t.CreatedByQCId, t.ApprovedByLeadId, t.ApprovedAt,
            t.ConfirmedByQCId, t.ConfirmedAt, t.Status.ToString(),
            qualityIssues);
    }

    private async Task<List<TransferQualityIssueDto>> GetUnrepairableQualityIssuesAsync(int batchId, int fromWorkshopId)
    {
        var works = await _unitOfWork.Works.FindAsync(
            x => x.BatchId == batchId
              && x.WorkshopId == fromWorkshopId
              && x.Status == WorkStatus.Rejected
              && x.UnrepairableQuantity > 0,
            new[] { "Staff", "Photos" });

        return works
            .OrderBy(x => x.SubmittedDate)
            .Select(x => new TransferQualityIssueDto(
                x.Id,
                x.StaffId,
                x.Staff?.Name ?? "",
                x.Quantity,
                x.PassedQuantity,
                x.RepairableQuantity,
                x.UnrepairableQuantity,
                x.RejectionNotes ?? "",
                x.ActualMaterialUsed,
                x.ReviewedAt,
                x.Photos
                    .Where(p => p.Type == WorkPhotoType.Rejection)
                    .Select(p => p.PhotoUrl)
                    .ToList()))
            .ToList();
    }

    private static int GetPassedQuantity(IEnumerable<Work> works)
    {
        return works.Sum(x => x.PassedQuantity);
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
}
