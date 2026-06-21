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
        var staff = await _unitOfWork.Users.GetByIdAsync(staffId)
            ?? throw new NotFoundException("Staff not found");

        if (staff.Role != UserRole.Staff)
            throw new ForbiddenException("Only staff can submit work");

        var workshopBw = await _unitOfWork.BatchWorkshops.FirstOrDefaultAsync(
            x => x.BatchId == dto.BatchId && x.WorkshopId == dto.WorkshopId,
            new[] { "Workshop" })
            ?? throw new NotFoundException("Workshop not in this batch");

        if (workshopBw.Workshop.RequiresMaterials && !workshopBw.MaterialsReceived)
            throw new BusinessRuleException("Workshop has not received materials yet");

        var work = new Work
        {
            BatchId = dto.BatchId,
            WorkshopId = dto.WorkshopId,
            StaffId = staffId,
            Quantity = dto.Quantity,
            PhotoUrl = dto.PhotoUrl,
            Status = WorkStatus.Submitted
        };

        await _unitOfWork.Works.AddAsync(work);

        var batchEntity = await _unitOfWork.Batches.GetByIdAsync(dto.BatchId);
        if (batchEntity != null)
        {
            batchEntity.Status = BatchStatus.UnderQCReview;
            _unitOfWork.Batches.Update(batchEntity);
        }

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkSubmittedAsync(dto.BatchId, dto.WorkshopId, new { WorkId = work.Id, Quantity = work.Quantity });

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
        work.RejectionReason = Enum.TryParse<RejectionReasonType>(dto.RejectionReason, true, out var r) ? r : RejectionReasonType.Other;
        work.RejectionNotes = dto.RejectionNotes;
        work.ReviewedByQCId = qcId;
        work.ReviewedAt = DateTime.UtcNow;
        _unitOfWork.Works.Update(work);

        await _unitOfWork.SaveChangesAsync();

        await _notifications.NotifyWorkRejectedAsync(work.BatchId, work.StaffId, new { WorkId = work.Id, Reason = dto.RejectionReason });

        return await MapToDto(dto.WorkId);
    }

    public async Task<IReadOnlyList<WorkDto>> GetWorksByBatchAsync(int batchId)
    {
        var works = await _unitOfWork.Works.FindAsync(x => x.BatchId == batchId,
            new[] { "Workshop", "Staff", "ReviewedByQC" });

        return works.Select(MapToDtoValue).ToList();
    }

    private async Task<WorkDto> MapToDto(int workId)
    {
        var work = await _unitOfWork.Works.FirstOrDefaultAsync(x => x.Id == workId,
            new[] { "Workshop", "Staff", "ReviewedByQC" })
            ?? throw new NotFoundException("Work not found");
        return MapToDtoValue(work);
    }

    private static WorkDto MapToDtoValue(Work w) => new(
        w.Id, w.BatchId, w.WorkshopId, w.Workshop?.Name ?? "",
        w.StaffId, w.Staff?.Name ?? "", w.Quantity, w.PhotoUrl,
        w.SubmittedDate, w.Status.ToString(),
        w.RejectionReason?.ToString(), w.RejectionNotes,
        w.ReviewedByQCId, w.ReviewedAt
    );
}
