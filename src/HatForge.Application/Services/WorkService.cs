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
        var batch = await _unitOfWork.Batches.GetByIdAsync(dto.BatchId)
            ?? throw new NotFoundException("Batch not found");

        if (batch.Status == BatchStatus.Assigned)
            throw new BusinessRuleException("Batch has not been planned yet");

        if (batch.Status == BatchStatus.Completed)
            throw new BusinessRuleException("Batch is already completed");

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

        // Check materials received if workshop requires them
        if (workshopBw.RequiresMaterials && !workshopBw.MaterialsReceived)
            throw new BusinessRuleException("Workshop has not received materials yet");

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
            }
        }

        // Prevent submitting new work while a previous submission is still pending QC review
        var pendingWork = await _unitOfWork.Works.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId
              && x.WorkshopId == dto.WorkshopId
              && x.Status == WorkStatus.Submitted);
        if (pendingWork != null)
            throw new BusinessRuleException("There is already a pending work submission awaiting QC review for this workshop");

        var work = new Work
        {
            BatchId = dto.BatchId,
            WorkshopId = dto.WorkshopId,
            StaffId = staffId,
            Quantity = dto.Quantity,
            Status = WorkStatus.Submitted
        };

        await _unitOfWork.Works.AddAsync(work);

        batch.Status = BatchStatus.UnderQCReview;
        _unitOfWork.Batches.Update(batch);

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

    public async Task<WorkDto> ApproveWorkAsync(int workId, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can approve work");

        var work = await _unitOfWork.Works.GetByIdAsync(workId)
            ?? throw new NotFoundException("Work not found");

        if (work.Status != WorkStatus.Submitted)
            throw new BusinessRuleException("Work is not in submitted state");

        work.Status = WorkStatus.Approved;
        work.ReviewedByQCId = qcId;
        work.ReviewedAt = DateTime.UtcNow;
        _unitOfWork.Works.Update(work);
        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkApprovedAsync(work.BatchId, work.StaffId, new { WorkId = work.Id });

        return await MapToDto(workId);
    }

    public async Task<WorkDto> RejectWorkAsync(RejectWorkDto dto, int qcId)
    {
        var qc = await _unitOfWork.Users.GetByIdAsync(qcId)
            ?? throw new NotFoundException("QC user not found");
        if (qc.Role != UserRole.QCWorkshop)
            throw new ForbiddenException("Only QC Workshop can reject work");

        var work = await _unitOfWork.Works.GetByIdAsync(dto.WorkId)
            ?? throw new NotFoundException("Work not found");

        if (work.Status != WorkStatus.Submitted)
            throw new BusinessRuleException("Work is not in submitted state");

        work.Status = WorkStatus.Rejected;
        work.RejectionNotes = dto.RejectionNotes;
        work.ReviewedByQCId = qcId;
        work.ReviewedAt = DateTime.UtcNow;
        _unitOfWork.Works.Update(work);

        await _unitOfWork.SaveChangesAsync();

        foreach (var url in dto.PhotoUrls)
        {
            await _unitOfWork.WorkPhotos.AddAsync(new WorkPhoto
            {
                WorkId = work.Id,
                PhotoUrl = url,
                Type = WorkPhotoType.Rejection
            });
        }

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkRejectedAsync(work.BatchId, work.StaffId, new { WorkId = work.Id });

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

    private static WorkDto MapToDtoValue(Work w) => new(
        w.Id, w.BatchId, w.WorkshopId, w.Workshop?.Name ?? "",
        w.StaffId, w.Staff?.Name ?? "", w.Quantity,
        w.Photos.Where(p => p.Type == WorkPhotoType.Submission).Select(p => p.PhotoUrl).ToList(),
        w.Photos.Where(p => p.Type == WorkPhotoType.Rejection).Select(p => p.PhotoUrl).ToList(),
        w.SubmittedDate, w.Status.ToString(),
        w.RejectionNotes,
        w.ReviewedByQCId, w.ReviewedAt
    );
}
